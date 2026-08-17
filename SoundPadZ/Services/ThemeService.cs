using System.Windows;
using System.Windows.Media;

namespace SoundPadZ.Services;

public sealed record ColorPreset(string Name, string Hex);

public static class ThemeService
{
    private static readonly Uri LightUri = new("pack://application:,,,/Themes/Palette.xaml");
    private static readonly Uri DarkUri = new("pack://application:,,,/Themes/Palette.Dark.xaml");

    public static readonly List<ColorPreset> Presets = new()
    {
        new ColorPreset("Голубой", "#90CAF9"),
        new ColorPreset("Изумрудный", "#81C784"),
        new ColorPreset("Фиолетовый", "#B39DDB"),
        new ColorPreset("Розовый", "#F48FB1"),
        new ColorPreset("Оранжевый", "#FFB74D"),
        new ColorPreset("Бирюзовый", "#80DEEA"),
        new ColorPreset("Коралловый", "#E57373"),
        new ColorPreset("Золотой", "#FFE082"),
    };

    public static void Apply(bool dark, string? accentHex = null)
    {
        var target = dark ? DarkUri : LightUri;
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source;
            if (source == null)
            {
                continue;
            }
            if (!source.ToString().Contains("Themes/Palette", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(source.ToString(), target.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                dictionaries.RemoveAt(i);
                dictionaries.Insert(i, new ResourceDictionary { Source = target });
            }
            break;
        }

        ApplyAccent(accentHex ?? SettingsService.Settings.AccentColor);
    }

    public static void ApplyAccent(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            hex = "#90CAF9";
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            ApplyAccentColor(color);
            SettingsService.Settings.AccentColor = hex;
        }
        catch
        {
            ApplyAccentColor(Color.FromRgb(0x90, 0xCA, 0xF9));
        }
    }

    private static void ApplyAccentColor(Color baseColor)
    {
        Color light = Color.FromRgb(
            (byte)Math.Clamp((int)(baseColor.R * 0.4 + 255 * 0.6), 0, 255),
            (byte)Math.Clamp((int)(baseColor.G * 0.4 + 255 * 0.6), 0, 255),
            (byte)Math.Clamp((int)(baseColor.B * 0.4 + 255 * 0.6), 0, 255)
        );
        Color dark = Color.FromRgb(
            (byte)Math.Clamp((int)(baseColor.R * 0.72), 0, 255),
            (byte)Math.Clamp((int)(baseColor.G * 0.72), 0, 255),
            (byte)Math.Clamp((int)(baseColor.B * 0.72), 0, 255)
        );

        var res = Application.Current.Resources;
        res["PrimaryHueLightColor"] = light;
        res["PrimaryHueMidColor"] = baseColor;
        res["PrimaryHueDarkColor"] = dark;
        res["PrimaryHueLightBrush"] = new SolidColorBrush(light);
        res["PrimaryHueMidBrush"] = new SolidColorBrush(baseColor);
        res["PrimaryHueDarkBrush"] = new SolidColorBrush(dark);
        res["AppAccentBrush"] = new SolidColorBrush(baseColor);
        res["AppAccentDarkBrush"] = new SolidColorBrush(dark);

        double luminance = (baseColor.R * 0.299 + baseColor.G * 0.587 + baseColor.B * 0.114);
        Color midFg = luminance < 140 ? Colors.White : Color.FromRgb(0x10, 0x2A, 0x44);
        Color lightFg = Color.FromRgb(0x10, 0x2A, 0x44);
        res["PrimaryHueMidForegroundColor"] = midFg;
        res["PrimaryHueMidForegroundBrush"] = new SolidColorBrush(midFg);
        res["PrimaryHueLightForegroundColor"] = lightFg;
        res["PrimaryHueLightForegroundBrush"] = new SolidColorBrush(lightFg);
    }
}
