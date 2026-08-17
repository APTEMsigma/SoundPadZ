using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SoundPadZ.Models;
using SoundPadZ.Services;
using SoundPadZ.ViewModels;

namespace SoundPadZ;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = SettingsService.Settings;
    private readonly LibraryService _library = new();
    private readonly AudioEngine _engine = new();
    private readonly ObservableCollection<SoundItemViewModel> _sounds = new();
    private readonly Dictionary<string, int> _hotkeyIds = new();
    private readonly SnackbarMessageQueue _queue = new(TimeSpan.FromSeconds(2.5));
    private int _nextHotkeyId = 0xA000;
    private HotkeyService? _hotkeys;
    private bool _loading = true;
    private bool _langGuard;

    public MainWindow()
    {
        InitializeComponent();
        _library.Load();
        MainSnackbar.MessageQueue = _queue;
        LstSounds.ItemsSource = _sounds;
        _sounds.CollectionChanged += (_, _) => UpdateEmptyState();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _hotkeys = new HotkeyService(this);

        ApplyTheme();
        InitLanguage();
        InitDevices();

        SlMonitorVol.Value = _settings.MonitorVolume;
        SlMicVol.Value = _settings.MicVolume;
        UpdateAutoStartButton(StartupService.IsAutoStartEnabled());
        _loading = false;
        _engine.SetMonitorVolume((float)_settings.MonitorVolume);
        _engine.SetMicVolume((float)_settings.MicVolume);

        foreach (var item in _library.Items)
        {
            var vm = new SoundItemViewModel(item, _engine);
            _sounds.Add(vm);
            RegisterHotkeyFor(vm, silent: true);
        }
        UpdateEmptyState();

        Loc.LanguageChanged += () => UpdateAutoStartButton(StartupService.IsAutoStartEnabled());

        if (App.SelfTest)
        {
            try
            {
                File.WriteAllText(Path.Combine(AppData.Dir, "selftest.log"),
                    "SELFTEST OK\r\n" +
                    $"lang={_settings.Language}\r\n" +
                    $"theme={(_settings.DarkTheme ? "dark" : "light")}\r\n" +
                    $"outputOk={_engine.OutputOk}\r\n" +
                    $"outputDevice={_engine.OutputDeviceId ?? "-"}\r\n" +
                    $"micEnabled={_engine.MicEnabled}\r\n" +
                    $"sounds={_sounds.Count}");
            }
            catch
            {
                // ignore: selftest result is best-effort
            }
            Application.Current.Shutdown(0);
            return;
        }

        if (_settings.ShowWelcomeDialog)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dialog = new Dialogs.WelcomeDialog { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    if (dialog.DontShowAgain)
                    {
                        _settings.ShowWelcomeDialog = false;
                        SettingsService.Save();
                    }
                }
            }));
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.OutputDeviceId = (CbOutput.SelectedItem as DevInfo)?.Id;
        _settings.MonitorDeviceId = (CbMonitor.SelectedItem as DevInfo)?.Id;
        _settings.MicDeviceId = (CbMic.SelectedItem as DevInfo)?.Id;
        _settings.MicEnabled = TgMic.IsChecked == true;
        _settings.MicVolume = SlMicVol.Value;
        _settings.MonitorVolume = SlMonitorVol.Value;
        SettingsService.Save();
        _library.Save();
        _hotkeys?.Dispose();
        _engine.Dispose();
    }

    // ---------- theme ----------

    private void PaletteButton_Click(object sender, RoutedEventArgs e)
    {
        PaletteMenu.PlacementTarget = BtnPalette;
        PaletteMenu.IsOpen = true;
    }

    private void AccentMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string hex)
        {
            ThemeService.ApplyAccent(hex);
            SettingsService.Settings.AccentColor = hex;
            SettingsService.Save();
        }
    }

    private void CustomColorMenu_Click(object sender, RoutedEventArgs e)
    {
        var currentHex = SettingsService.Settings.AccentColor;
        var dialog = new Dialogs.CustomColorDialog(currentHex) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            var chosen = dialog.SelectedHex;
            ThemeService.ApplyAccent(chosen);
            SettingsService.Settings.AccentColor = chosen;
            SettingsService.Save();
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Settings.DarkTheme = !SettingsService.Settings.DarkTheme;
        SettingsService.Save();
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var dark = SettingsService.Settings.DarkTheme;
        ThemeService.Apply(dark, SettingsService.Settings.AccentColor);
        ThemeIcon.Kind = dark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
    }

    private void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        var current = StartupService.IsAutoStartEnabled();
        var target = !current;
        if (StartupService.SetAutoStart(target))
        {
            UpdateAutoStartButton(target);
            _queue.Enqueue(target ? Loc.Get("AutoStartEnabled") : Loc.Get("AutoStartDisabled"));
        }
    }

    private void UpdateAutoStartButton(bool enabled)
    {
        AutoStartIcon.Kind = enabled ? PackIconKind.RocketLaunch : PackIconKind.RocketLaunchOutline;
        AutoStartIcon.Opacity = enabled ? 1.0 : 0.6;
        BtnAutoStart.ToolTip = enabled ? Loc.Get("AutoStartEnabledTooltip") : Loc.Get("AutoStartDisabledTooltip");
    }

    private void Welcome_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.WelcomeDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.DontShowAgain)
            {
                _settings.ShowWelcomeDialog = false;
                SettingsService.Save();
            }
        }
    }

    // ---------- language ----------

    private void InitLanguage()
    {
        _langGuard = true;
        if (_settings.Language == "en")
        {
            RbEn.IsChecked = true;
        }
        else
        {
            RbRu.IsChecked = true;
        }
        _langGuard = false;
    }

    private void RbRu_Checked(object sender, RoutedEventArgs e)
    {
        if (!_langGuard && _settings.Language != "ru")
        {
            Loc.Apply("ru");
            SettingsService.Save();
        }
    }

    private void RbEn_Checked(object sender, RoutedEventArgs e)
    {
        if (!_langGuard && _settings.Language != "en")
        {
            Loc.Apply("en");
            SettingsService.Save();
        }
    }

    // ---------- devices ----------

    private void InitDevices()
    {
        _loading = true;

        var outputs = Devices.List(DataFlow.Render);
        CbOutput.ItemsSource = outputs;
        var selectedOutput = outputs.FirstOrDefault(d => d.Id == _settings.OutputDeviceId)
                          ?? outputs.FirstOrDefault(d => d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
                          ?? outputs.FirstOrDefault();
        CbOutput.SelectedItem = selectedOutput;

        var monitorList = new List<DevInfo>
        {
            new DevInfo { Id = "", Name = Loc.Get("MonitorNone") }
        };
        monitorList.AddRange(outputs);
        CbMonitor.ItemsSource = monitorList;

        var selectedMonitor = monitorList.FirstOrDefault(d => !string.IsNullOrEmpty(d.Id) && d.Id == _settings.MonitorDeviceId);
        if (selectedMonitor == null && selectedOutput != null && selectedOutput.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
        {
            selectedMonitor = monitorList.FirstOrDefault(d => !string.IsNullOrEmpty(d.Id) && d.Id != selectedOutput.Id);
        }
        CbMonitor.SelectedItem = selectedMonitor ?? monitorList[0];

        var mics = Devices.List(DataFlow.Capture);
        CbMic.ItemsSource = mics;
        var selectedMic = mics.FirstOrDefault(d => d.Id == _settings.MicDeviceId)
                       ?? mics.FirstOrDefault();
        CbMic.SelectedItem = selectedMic;

        var isCable = selectedOutput != null && (
            selectedOutput.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
            selectedOutput.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            selectedOutput.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase));

        TgMic.IsChecked = _settings.MicEnabled && selectedMic != null && isCable;
        _loading = false;

        ApplySelectedOutput();
        ApplySelectedMonitor();
        if (TgMic.IsChecked == true)
        {
            ApplyMicEnabled(true);
        }
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e) => InitDevices();

    private void CbOutput_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            ApplySelectedOutput();
        }
    }

    private void ApplySelectedOutput()
    {
        if (CbOutput.SelectedItem is DevInfo device)
        {
            _settings.OutputDeviceId = device.Id;
            _engine.SetOutputDevice(device.Id);
        }
    }

    private void CbMonitor_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading)
        {
            ApplySelectedMonitor();
        }
    }

    private void ApplySelectedMonitor()
    {
        if (CbMonitor.SelectedItem is DevInfo device)
        {
            _settings.MonitorDeviceId = device.Id;
            _engine.SetMonitorDevice(device.Id);
        }
    }

    private void CbMic_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        if (CbMic.SelectedItem is DevInfo device)
        {
            _settings.MicDeviceId = device.Id;
            if (_settings.MicEnabled)
            {
                _engine.SetMic(device.Id, true);
            }
        }
    }

    private void TgMic_Checked(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            ApplyMicEnabled(true);
        }
    }

    private void TgMic_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            ApplyMicEnabled(false);
        }
    }

    private void ApplyMicEnabled(bool enable)
    {
        if (enable && CbMic.SelectedItem is not DevInfo)
        {
            _queue.Enqueue(Loc.Get("MicNotFound"));
            TgMic.IsChecked = false;
            return;
        }

        if (enable && CbOutput.SelectedItem is DevInfo outDev)
        {
            var isCable = outDev.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
                          outDev.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                          outDev.Name.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase);
            if (!isCable)
            {
                _queue.Enqueue(Loc.Get("WarnMicFeedback"));
                _settings.MicEnabled = false;
                TgMic.IsChecked = false;
                _engine.SetMic(null, false);
                return;
            }
        }

        _settings.MicEnabled = enable;
        var id = (CbMic.SelectedItem as DevInfo)?.Id;
        _engine.SetMic(id, enable);
    }

    private void SlMonitorVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading)
        {
            return;
        }
        _settings.MonitorVolume = e.NewValue;
        _engine.SetMonitorVolume((float)e.NewValue);
    }

    private void SlMicVol_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading)
        {
            return;
        }
        _settings.MicVolume = e.NewValue;
        _engine.SetMicVolume((float)e.NewValue);
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        _engine.StopAllSounds();
        foreach (var vm in _sounds)
        {
            vm.ForceStopped();
        }
    }

    // ---------- library ----------

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.wma;*.aiff;*.mp4|All files|*.*"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var added = 0;
        foreach (var file in files)
        {
            try
            {
                double duration;
                using (var reader = new AudioFileReader(file))
                {
                    duration = reader.TotalTime.TotalSeconds;
                }
                var item = _library.AddFile(file, duration);
                var vm = new SoundItemViewModel(item, _engine);
                _sounds.Add(vm);
                RegisterHotkeyFor(vm, silent: true);
                added++;
            }
            catch
            {
                _queue.Enqueue(Loc.F("ErrFileCopy", Path.GetFileName(file)));
            }
        }
        if (added > 0)
        {
            _library.Save();
            _queue.Enqueue($"{Loc.Get("Added")} ({added})");
        }
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUrlDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultPath != null)
        {
            var item = _library.RegisterDownloaded(dialog.ResultPath, dialog.ResultDuration, dialog.ResultName, dialog.SourceUrl);
            var vm = new SoundItemViewModel(item, _engine);
            _sounds.Add(vm);
            RegisterHotkeyFor(vm, silent: true);
            _library.Save();
            _queue.Enqueue(Loc.Get("Added"));
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            AddFiles(files);
        }
    }

    // ---------- tiles ----------

    private static SoundItemViewModel? ViewModelOf(object sender)
    {
        return (sender as FrameworkElement)?.DataContext as SoundItemViewModel;
    }

    private void TilePlay_Click(object sender, RoutedEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm == null)
        {
            return;
        }
        try
        {
            vm.TogglePlay();
        }
        catch
        {
            vm.Stop();
            _queue.Enqueue(Loc.Get("ErrFormat"));
        }
    }

    private void TileClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm == null) return;

        vm.Item.HotkeyMods = 0;
        vm.Item.HotkeyKey = 0;
        vm.IsRecordingHotkey = false;
        UnregisterHotkeyFor(vm);
        _library.Save();
        vm.ApplyEdited();
        _queue.Enqueue(Loc.Get("HotkeyCleared"));
    }

    private void TileDelete_Click(object sender, RoutedEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm == null)
        {
            return;
        }

        var confirm = MessageBox.Show(this, Loc.F("ConfirmDelete", vm.Name), "SoundPadZ",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        vm.Stop();
        UnregisterHotkeyFor(vm);
        _library.Remove(vm.Item);
        _sounds.Remove(vm);
        _library.Save();
        _queue.Enqueue(Loc.Get("Deleted"));
    }

    private void TileHotkey_Click(object sender, RoutedEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm == null) return;

        foreach (var other in _sounds)
        {
            if (other != vm && other.IsRecordingHotkey)
            {
                other.IsRecordingHotkey = false;
            }
        }

        vm.IsRecordingHotkey = !vm.IsRecordingHotkey;
        if (vm.IsRecordingHotkey && sender is FrameworkElement el)
        {
            el.Focus();
        }
    }

    private void TileHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm == null || !vm.IsRecordingHotkey) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.None) return;

        if (key == Key.Escape)
        {
            vm.IsRecordingHotkey = false;
            e.Handled = true;
            return;
        }

        if (key is Key.Back or Key.Delete)
        {
            vm.Item.HotkeyMods = 0;
            vm.Item.HotkeyKey = 0;
            vm.IsRecordingHotkey = false;
            UnregisterHotkeyFor(vm);
            _library.Save();
            vm.ApplyEdited();
            _queue.Enqueue(Loc.Get("HotkeyCleared"));
            e.Handled = true;
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mods |= HotkeyService.MOD_CONTROL;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mods |= HotkeyService.MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= HotkeyService.MOD_SHIFT;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= HotkeyService.MOD_WIN;

        if (key is Key.LeftAlt or Key.RightAlt) { mods &= ~HotkeyService.MOD_ALT; vk = 0x12; }
        else if (key is Key.LeftCtrl or Key.RightCtrl) { mods &= ~HotkeyService.MOD_CONTROL; vk = 0x11; }
        else if (key is Key.LeftShift or Key.RightShift) { mods &= ~HotkeyService.MOD_SHIFT; vk = 0x10; }
        else if (key is Key.LWin or Key.RWin) { mods &= ~HotkeyService.MOD_WIN; vk = 0x5B; }

        var conflict = _sounds.FirstOrDefault(v =>
            v != vm && v.Item.HotkeyMods == mods && v.Item.HotkeyKey == (uint)vk);
        if (conflict != null)
        {
            _queue.Enqueue(Loc.Get("HotkeyTaken"));
        }
        else
        {
            vm.Item.HotkeyMods = mods;
            vm.Item.HotkeyKey = (uint)vk;
            RegisterHotkeyFor(vm);
            _library.Save();
            _queue.Enqueue($"{Loc.Get("HotkeyAssigned")}: {HotkeyService.ComboText(mods, (uint)vk)}");
        }

        vm.IsRecordingHotkey = false;
        vm.ApplyEdited();
        e.Handled = true;
    }

    private void TileHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        var vm = ViewModelOf(sender);
        if (vm != null && vm.IsRecordingHotkey)
        {
            vm.IsRecordingHotkey = false;
        }
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = _sounds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- hotkeys ----------

    private int HotkeyIdOf(string soundId)
    {
        if (!_hotkeyIds.TryGetValue(soundId, out var id))
        {
            id = _nextHotkeyId++;
            _hotkeyIds[soundId] = id;
        }
        return id;
    }

    private void RegisterHotkeyFor(SoundItemViewModel vm, bool silent = false)
    {
        if (_hotkeys == null)
        {
            return;
        }

        var id = HotkeyIdOf(vm.Item.Id);
        _hotkeys.Unregister(id);

        if (vm.Item.HotkeyKey == 0)
        {
            return;
        }

        var ok = _hotkeys.Register(id, vm.Item.HotkeyMods, vm.Item.HotkeyKey, () =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    vm.TogglePlay();
                }
                catch
                {
                    vm.Stop();
                }
            });
        });

        if (!ok && !silent)
        {
            _queue.Enqueue(Loc.Get("HotkeyTaken"));
        }
    }

    private void UnregisterHotkeyFor(SoundItemViewModel vm)
    {
        if (_hotkeys == null || !_hotkeyIds.TryGetValue(vm.Item.Id, out var id))
        {
            return;
        }
        _hotkeys.Unregister(id);
    }
}
