namespace SoundPadZ.Models;

public sealed class SoundItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string? Url { get; set; }
    public double Duration { get; set; }
    public double Volume { get; set; } = 0.9;
    public bool Loop { get; set; }
    public uint HotkeyMods { get; set; }
    public uint HotkeyKey { get; set; }
}

public sealed class AppSettings
{
    public string Language { get; set; } = "ru";
    public bool DarkTheme { get; set; }
    public string AccentColor { get; set; } = "#90CAF9";
    public string? OutputDeviceId { get; set; }
    public string? MonitorDeviceId { get; set; }
    public string? MicDeviceId { get; set; }
    public bool MicEnabled { get; set; }
    public double MicVolume { get; set; } = 0.9;
    public double MonitorVolume { get; set; } = 0.85;
    public bool ShowWelcomeDialog { get; set; } = true;
}
