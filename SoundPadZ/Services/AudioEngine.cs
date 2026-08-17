using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SoundPadZ.Services;

/// <summary>
/// Decoded audio in the mixer's format (48 kHz stereo float), ready for instant playback.
/// </summary>
public sealed class CachedSound
{
    public float[] Data { get; }
    public WaveFormat Format { get; }
    public TimeSpan Duration { get; }

    public CachedSound(string path, WaveFormat target)
    {
        using var reader = new AudioFileReader(path);

        ISampleProvider source = reader;
        if (source.WaveFormat.Channels == 1 && target.Channels == 2)
        {
            source = new MonoToStereoSampleProvider(source);
        }
        else if (source.WaveFormat.Channels > 2 && target.Channels == 2)
        {
            source = new MultiplexingSampleProvider(new[] { source }, 2);
        }

        if (source.WaveFormat.SampleRate != target.SampleRate)
        {
            source = new WdlResamplingSampleProvider(source, target.SampleRate);
        }

        var buffer = new float[target.SampleRate * target.Channels];
        var samples = new List<float>(target.SampleRate * target.Channels * 10);
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        if (samples.Count == 0)
        {
            throw new InvalidOperationException("Audio file contains no samples: " + path);
        }

        Data = samples.ToArray();
        Format = target;
        Duration = TimeSpan.FromSeconds(Data.Length / (double)(target.SampleRate * target.Channels));
    }
}

public sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sound;
    private readonly object _sync = new();
    private int _position;
    private float _volume;
    private bool _loop;
    private bool _endRaised;

    public event Action? Ended;

    public WaveFormat WaveFormat => _sound.Format;
    public float Volume { get => _volume; set => _volume = value; }
    public bool Loop { get => _loop; set => _loop = value; }

    public CachedSoundSampleProvider(CachedSound sound, float volume, bool loop)
    {
        _sound = sound;
        _volume = volume;
        _loop = loop;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_sync)
        {
            var total = 0;
            while (total < count)
            {
                var remaining = _sound.Data.Length - _position;
                if (remaining <= 0)
                {
                    if (_loop && _sound.Data.Length > 0)
                    {
                        _position = 0;
                        remaining = _sound.Data.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                var toCopy = Math.Min(count - total, remaining);
                for (var i = 0; i < toCopy; i++)
                {
                    buffer[offset + total + i] = _sound.Data[_position + i] * _volume;
                }
                _position += toCopy;
                total += toCopy;
            }

            if (total < count)
            {
                Array.Clear(buffer, offset + total, count - total);
            }

            if (_position >= _sound.Data.Length && !_loop && !_endRaised)
            {
                _endRaised = true;
                ThreadPool.QueueUserWorkItem(_ => Ended?.Invoke());
            }

            return total;
        }
    }
}

public sealed class SoundPlaybackHandle
{
    public CachedSoundSampleProvider CableProvider { get; }
    public CachedSoundSampleProvider? MonitorProvider { get; }

    public SoundPlaybackHandle(CachedSoundSampleProvider cable, CachedSoundSampleProvider? monitor)
    {
        CableProvider = cable;
        MonitorProvider = monitor;
    }

    public float Volume
    {
        get => CableProvider.Volume;
        set
        {
            CableProvider.Volume = value;
            if (MonitorProvider != null)
            {
                MonitorProvider.Volume = value;
            }
        }
    }

    public bool Loop
    {
        get => CableProvider.Loop;
        set
        {
            CableProvider.Loop = value;
            if (MonitorProvider != null)
            {
                MonitorProvider.Loop = value;
            }
        }
    }
}

/// <summary>
/// Central audio graph: outputs sounds + mic to Virtual Cable (Discord/game),
/// and outputs sounds to monitor device (Headphones) with dedicated software-level volume control.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    public const int SampleRate = 48000;
    private const int CacheLimit = 16;

    private readonly object _sync = new();
    private readonly object _activeLock = new();
    private readonly MixingSampleProvider _cableMixer;
    private readonly MixingSampleProvider _monitorMixer;
    private readonly VolumeSampleProvider _monitorVolumeProvider;
    private readonly Dictionary<string, CachedSound> _cache = new();
    private readonly LinkedList<string> _cacheOrder = new();
    private readonly List<SoundPlaybackHandle> _active = new();
    private readonly Dictionary<string, CachedSound> _cacheGuard = new();

    private WasapiOut? _output;
    private WasapiOut? _monitorOutput;
    private WasapiCapture? _micCapture;
    private BufferedWaveProvider? _micBuffer;
    private ISampleProvider? _micInput;
    private VolumeSampleProvider? _micVolume;

    private float _monitorVol = 0.85f;
    private float _micVol = 1f;

    public bool OutputOk { get; private set; }
    public string? OutputDeviceId { get; private set; }
    public bool MonitorOk { get; private set; }
    public string? MonitorDeviceId { get; private set; }
    public bool MicEnabled { get; private set; }
    public string? MicDeviceId { get; private set; }

    public AudioEngine()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
        _cableMixer = new MixingSampleProvider(format) { ReadFully = true };
        _monitorMixer = new MixingSampleProvider(format) { ReadFully = true };
        _monitorVolumeProvider = new VolumeSampleProvider(_monitorMixer) { Volume = _monitorVol };
    }

    private static WasapiOut? CreateWasapiOut(MMDevice device, ISampleProvider source)
    {
        try
        {
            var mixFormat = device.AudioClient.MixFormat;
            ISampleProvider chain = source;

            if (chain.WaveFormat.SampleRate != mixFormat.SampleRate)
            {
                chain = new WdlResamplingSampleProvider(chain, mixFormat.SampleRate);
            }

            if (chain.WaveFormat.Channels != mixFormat.Channels)
            {
                if (chain.WaveFormat.Channels == 1 && mixFormat.Channels == 2)
                {
                    chain = new MonoToStereoSampleProvider(chain);
                }
                else if (mixFormat.Channels > 2)
                {
                    chain = new MultiplexingSampleProvider(new[] { chain }, mixFormat.Channels);
                }
            }

            IWaveProvider waveProvider;
            if (mixFormat.Encoding == WaveFormatEncoding.IeeeFloat ||
                (mixFormat is WaveFormatExtensible ext && (ext.SubFormat == AudioSubtypes.MFAudioFormat_Float || ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))))
            {
                waveProvider = chain.ToWaveProvider();
            }
            else if (mixFormat.BitsPerSample == 16)
            {
                waveProvider = chain.ToWaveProvider16();
            }
            else
            {
                waveProvider = new MediaFoundationResampler(chain.ToWaveProvider(), mixFormat);
            }

            var wasapi = new WasapiOut(device, AudioClientShareMode.Shared, true, 70);
            wasapi.Init(waveProvider);
            wasapi.Play();
            return wasapi;
        }
        catch
        {
            return null;
        }
    }

    public void SetOutputDevice(string? deviceId)
    {
        lock (_sync)
        {
            OutputDeviceId = deviceId;
            OutputOk = false;

            try { _output?.Stop(); } catch { }
            try { _output?.Dispose(); } catch { }
            _output = null;

            var device = TryGetDevice(deviceId, DataFlow.Render);
            if (device == null)
            {
                return;
            }

            _output = CreateWasapiOut(device, _cableMixer);
            OutputOk = _output != null;
        }
    }

    public void SetMonitorDevice(string? deviceId)
    {
        lock (_sync)
        {
            MonitorDeviceId = deviceId;
            MonitorOk = false;

            try { _monitorOutput?.Stop(); } catch { }
            try { _monitorOutput?.Dispose(); } catch { }
            _monitorOutput = null;

            if (string.IsNullOrEmpty(deviceId) || string.Equals(deviceId, OutputDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var device = TryGetDevice(deviceId, DataFlow.Render);
            if (device == null)
            {
                return;
            }

            _monitorOutput = CreateWasapiOut(device, _monitorVolumeProvider);
            MonitorOk = _monitorOutput != null;
        }
    }

    public void SetMic(string? deviceId, bool enabled)
    {
        lock (_sync)
        {
            MicDeviceId = deviceId;
            MicEnabled = enabled && !string.IsNullOrEmpty(deviceId);

            TearDownMic();

            if (!MicEnabled)
            {
                return;
            }

            var device = TryGetDevice(deviceId, DataFlow.Capture);
            if (device == null)
            {
                MicEnabled = false;
                return;
            }

            try
            {
                _micCapture = new WasapiCapture(device);
                _micBuffer = new BufferedWaveProvider(_micCapture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true,
                    ReadFully = true,
                    BufferDuration = TimeSpan.FromMilliseconds(500)
                };

                ISampleProvider chain = _micBuffer.ToSampleProvider();
                if (chain.WaveFormat.Channels == 1)
                {
                    chain = new MonoToStereoSampleProvider(chain);
                }
                else if (chain.WaveFormat.Channels > 2)
                {
                    chain = new MultiplexingSampleProvider(new[] { chain }, 2);
                }

                if (chain.WaveFormat.SampleRate != SampleRate)
                {
                    chain = new WdlResamplingSampleProvider(chain, SampleRate);
                }

                _micVolume = new VolumeSampleProvider(chain) { Volume = _micVol };
                _micInput = _micVolume;
                _cableMixer.AddMixerInput(_micInput);

                _micCapture.DataAvailable += OnMicData;
                _micCapture.StartRecording();
            }
            catch
            {
                TearDownMic();
                MicEnabled = false;
            }
        }
    }

    public void SetMicEnabled(bool enabled) => SetMic(MicDeviceId, enabled);

    public void SetMicVolume(float value)
    {
        lock (_sync)
        {
            _micVol = value;
            if (_micVolume != null)
            {
                _micVolume.Volume = value;
            }
        }
    }

    public void SetMonitorVolume(float value)
    {
        _monitorVol = Math.Clamp(value, 0f, 1.5f);
        lock (_sync)
        {
            _monitorVolumeProvider.Volume = _monitorVol;
        }
    }

    public CachedSound GetSound(string path)
    {
        lock (_cacheGuard)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                _cacheOrder.Remove(path);
                _cacheOrder.AddFirst(path);
                return cached;
            }
        }

        var sound = new CachedSound(path, _cableMixer.WaveFormat);

        lock (_cacheGuard)
        {
            _cache[path] = sound;
            _cacheOrder.AddFirst(path);
            while (_cacheOrder.Count > CacheLimit)
            {
                var oldest = _cacheOrder.Last!.Value;
                _cacheOrder.RemoveLast();
                _cache.Remove(oldest);
            }
        }
        return sound;
    }

    public SoundPlaybackHandle PlaySound(CachedSound sound, float volume, bool loop, Action onEnded)
    {
        var cableProv = new CachedSoundSampleProvider(sound, volume, loop);
        CachedSoundSampleProvider? monitorProv = null;

        lock (_sync)
        {
            if (_monitorOutput != null && MonitorOk)
            {
                monitorProv = new CachedSoundSampleProvider(sound, volume, loop);
            }
        }

        var handle = new SoundPlaybackHandle(cableProv, monitorProv);

        int endedCount = 0;
        void HandleEnded()
        {
            if (Interlocked.Increment(ref endedCount) == 1)
            {
                lock (_activeLock)
                {
                    _active.Remove(handle);
                }
                try { _cableMixer.RemoveMixerInput(cableProv); } catch { }
                if (monitorProv != null)
                {
                    try { _monitorMixer.RemoveMixerInput(monitorProv); } catch { }
                }
                onEnded();
            }
        }

        cableProv.Ended += HandleEnded;
        if (monitorProv != null)
        {
            monitorProv.Ended += HandleEnded;
        }

        lock (_activeLock)
        {
            _active.Add(handle);
        }

        _cableMixer.AddMixerInput(cableProv);
        if (monitorProv != null)
        {
            _monitorMixer.AddMixerInput(monitorProv);
        }

        return handle;
    }

    public void StopSound(SoundPlaybackHandle? handle)
    {
        if (handle == null) return;
        lock (_activeLock)
        {
            _active.Remove(handle);
        }
        try { _cableMixer.RemoveMixerInput(handle.CableProvider); } catch { }
        if (handle.MonitorProvider != null)
        {
            try { _monitorMixer.RemoveMixerInput(handle.MonitorProvider); } catch { }
        }
    }

    public void StopAllSounds()
    {
        SoundPlaybackHandle[] snapshot;
        lock (_activeLock)
        {
            snapshot = _active.ToArray();
            _active.Clear();
        }
        foreach (var handle in snapshot)
        {
            try { _cableMixer.RemoveMixerInput(handle.CableProvider); } catch { }
            if (handle.MonitorProvider != null)
            {
                try { _monitorMixer.RemoveMixerInput(handle.MonitorProvider); } catch { }
            }
        }
    }

    private void OnMicData(object? sender, WaveInEventArgs e)
    {
        _micBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void TearDownMic()
    {
        if (_micCapture != null)
        {
            try { _micCapture.DataAvailable -= OnMicData; } catch { }
            try { _micCapture.StopRecording(); } catch { }
            try { _micCapture.Dispose(); } catch { }
            _micCapture = null;
        }

        if (_micInput != null)
        {
            try { _cableMixer.RemoveMixerInput(_micInput); } catch { }
            _micInput = null;
        }

        _micBuffer = null;
        _micVolume = null;
    }

    private static MMDevice? TryGetDevice(string? id, DataFlow flow)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        try
        {
            var device = new MMDeviceEnumerator().GetDevice(id);
            return device.DataFlow == flow && device.State == DeviceState.Active ? device : null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            StopAllSounds();
            TearDownMic();
            MicEnabled = false;
            try { _output?.Stop(); } catch { }
            try { _output?.Dispose(); } catch { }
            _output = null;
            OutputOk = false;

            try { _monitorOutput?.Stop(); } catch { }
            try { _monitorOutput?.Dispose(); } catch { }
            _monitorOutput = null;
            MonitorOk = false;
        }
    }
}
