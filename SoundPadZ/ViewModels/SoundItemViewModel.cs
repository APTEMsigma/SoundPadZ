using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using MaterialDesignThemes.Wpf;
using SoundPadZ.Models;
using SoundPadZ.Services;

namespace SoundPadZ.ViewModels;

public sealed class SoundItemViewModel : INotifyPropertyChanged
{
    public SoundItem Item { get; }
    private readonly AudioEngine _engine;
    private SoundPlaybackHandle? _provider;
    private bool _isPlaying;
    private bool _isRecordingHotkey;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SoundItemViewModel(SoundItem item, AudioEngine engine)
    {
        Item = item;
        _engine = engine;
        Loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(HotkeyDisplayButtonText));
        OnPropertyChanged(nameof(HotkeyText));
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayPauseIcon));
        }
    }

    public bool IsRecordingHotkey
    {
        get => _isRecordingHotkey;
        set
        {
            if (_isRecordingHotkey == value) return;
            _isRecordingHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HotkeyDisplayButtonText));
            OnPropertyChanged(nameof(IsHotkeyAssigned));
        }
    }

    public bool IsHotkeyAssigned => Item.HotkeyKey != 0;
    public Visibility ClearHotkeyVisibility => IsHotkeyAssigned ? Visibility.Visible : Visibility.Collapsed;

    public string HotkeyDisplayButtonText
    {
        get
        {
            if (IsRecordingHotkey)
            {
                return Loc.Get("PressAnyKey");
            }
            if (Item.HotkeyKey != 0)
            {
                return HotkeyService.ComboText(Item.HotkeyMods, Item.HotkeyKey);
            }
            return Loc.Get("AddHotkey");
        }
    }

    public string Name => Item.Name;

    public PackIconKind PlayPauseIcon => IsPlaying ? PackIconKind.Stop : PackIconKind.Play;

    public string HotkeyText => HotkeyService.ComboText(Item.HotkeyMods, Item.HotkeyKey);

    public Visibility HotkeyVisibility =>
        Item.HotkeyKey == 0 ? Visibility.Collapsed : Visibility.Visible;

    public string VolumePercentText => $"{(int)(Volume * 100)}%";

    public double Volume
    {
        get => Item.Volume;
        set
        {
            Item.Volume = Math.Round(value, 2);
            if (_provider != null)
            {
                _provider.Volume = (float)Item.Volume;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercentText));
        }
    }

    public bool Loop
    {
        get => Item.Loop;
        set
        {
            Item.Loop = value;
            if (_provider != null)
            {
                _provider.Loop = value;
            }
        }
    }

    public string DurationText
    {
        get
        {
            var t = TimeSpan.FromSeconds(Item.Duration);
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes}:{t.Seconds:00}";
        }
    }

    public string FullPath => Path.Combine(AppData.SoundsDir, Item.File);
    private CancellationTokenSource? _playCts;

    public void TogglePlay()
    {
        if (IsPlaying)
        {
            Stop();
            return;
        }

        try
        {
            Stop();
            var sound = _engine.GetSound(FullPath);
            var duration = sound.Duration > TimeSpan.Zero ? sound.Duration : TimeSpan.FromSeconds(Math.Max(Item.Duration, 1.0));

            var cts = new CancellationTokenSource();
            _playCts = cts;

            _provider = _engine.PlaySound(sound, (float)Item.Volume, Item.Loop, () =>
            {
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (_playCts == cts)
                    {
                        _playCts = null;
                        _provider = null;
                        IsPlaying = false;
                    }
                }));
            });
            IsPlaying = true;

            if (!Item.Loop)
            {
                var delayMs = (int)(duration.TotalMilliseconds + 350);
                Task.Delay(delayMs, cts.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            if (_playCts == cts && IsPlaying)
                            {
                                Stop();
                            }
                        }));
                    }
                }, TaskScheduler.Default);
            }
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        _playCts?.Cancel();
        _playCts = null;
        _engine.StopSound(_provider);
        _provider = null;
        IsPlaying = false;
    }

    /// <summary>After StopAllSounds the providers are gone on the engine side; drop UI references.</summary>
    public void ForceStopped()
    {
        _playCts?.Cancel();
        _playCts = null;
        _provider = null;
        IsPlaying = false;
    }

    /// <summary>Push edited values into a possibly-playing provider and refresh bindings.</summary>
    public void ApplyEdited()
    {
        if (_provider != null)
        {
            _provider.Volume = (float)Item.Volume;
            _provider.Loop = Item.Loop;
        }
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercentText));
        OnPropertyChanged(nameof(Loop));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(HotkeyVisibility));
        OnPropertyChanged(nameof(HotkeyDisplayButtonText));
        OnPropertyChanged(nameof(IsHotkeyAssigned));
        OnPropertyChanged(nameof(ClearHotkeyVisibility));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
