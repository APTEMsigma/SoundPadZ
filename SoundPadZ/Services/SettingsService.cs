using System.IO;
using System.Text.Json;
using SoundPadZ.Models;

namespace SoundPadZ.Services;

public static class AppData
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundPadZ");

    public static readonly string SoundsDir = Path.Combine(Dir, "sounds");

    static AppData()
    {
        try
        {
            Directory.CreateDirectory(SoundsDir);
        }
        catch
        {
            // if %APPDATA% is unavailable the app still starts; saving will simply fail
        }
    }
}

public static class SettingsService
{
    private static string SettingsPath => Path.Combine(AppData.Dir, "settings.json");

    public static AppSettings Settings { get; private set; } = Load();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (s != null)
                {
                    return s;
                }
            }
        }
        catch
        {
            // corrupted settings fall back to defaults
        }
        return new AppSettings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppData.Dir);
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // read-only disk or locked file: keep running with current settings
        }
    }
}
