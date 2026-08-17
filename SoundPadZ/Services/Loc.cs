using System.Windows;

namespace SoundPadZ.Services;

public static class Loc
{
    public static event Action? LanguageChanged;

    public static void Apply(string lang)
    {
        lang = lang == "en" ? "en" : "ru";

        var resources = Application.Current.Resources;
        for (var i = resources.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var source = resources.MergedDictionaries[i].Source?.ToString();
            if (source != null && source.Contains("Strings.", StringComparison.Ordinal))
            {
                resources.MergedDictionaries.RemoveAt(i);
            }
        }

        resources.MergedDictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Strings.{lang}.xaml")
        });

        SettingsService.Settings.Language = lang;
        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    public static string F(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }
}
