using System.IO;
using System.Windows;
using SoundPadZ.Services;

namespace SoundPadZ;

public partial class App : Application
{
    public static string[] Args { get; private set; } = Array.Empty<string>();

    public static bool SelfTest => Args.Contains("--selftest", StringComparer.OrdinalIgnoreCase);

    protected override void OnStartup(StartupEventArgs e)
    {
        Args = e.Args.ToArray();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            Loc.Apply(SettingsService.Settings.Language);
            ThemeService.Apply(SettingsService.Settings.DarkTheme, SettingsService.Settings.AccentColor);
        }
        catch
        {
            // localization/theming are best-effort at startup; the app still runs with defaults
        }

        base.OnStartup(e);
    }

    private bool _inException;

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        if (_inException)
        {
            return;
        }
        _inException = true;
        try
        {
            try
            {
                File.AppendAllText(Path.Combine(AppData.Dir, "log.txt"),
                    $"[{DateTime.Now:u}] {e.Exception}\r\n\r\n");
            }
            catch
            {
                // nowhere to log
            }

            if (SelfTest)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppData.Dir, "selftest.log"),
                        "SELFTEST FAIL\r\n" + e.Exception);
                }
                catch
                {
                    // ignore
                }
                Shutdown(-1);
                e.Handled = true;
                return;
            }

            MessageBox.Show(e.Exception.Message, "SoundPadZ", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
        finally
        {
            _inException = false;
        }
    }
}
