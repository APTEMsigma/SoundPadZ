using System.IO;
using System.Text.Json;
using SoundPadZ.Models;

namespace SoundPadZ.Services;

public sealed class LibraryService
{
    public List<SoundItem> Items { get; } = new();

    private static string LibraryPath => Path.Combine(AppData.Dir, "library.json");

    public void Load()
    {
        try
        {
            if (File.Exists(LibraryPath))
            {
                var data = JsonSerializer.Deserialize<List<SoundItem>>(File.ReadAllText(LibraryPath));
                if (data != null)
                {
                    Items.Clear();
                    Items.AddRange(data);
                }
            }
        }
        catch
        {
            // corrupted library: start empty rather than crash
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppData.Dir);
            File.WriteAllText(LibraryPath,
                JsonSerializer.Serialize(Items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // read-only disk or locked file: keep running with in-memory library
        }
    }

    public SoundItem AddFile(string sourcePath, double durationSeconds)
    {
        var name = Downloader.Sanitize(Path.GetFileNameWithoutExtension(sourcePath));
        if (name.Length > 40)
        {
            name = name[..40];
        }

        var fileName = $"{name}_{Guid.NewGuid().ToString("N")[..6]}{Path.GetExtension(sourcePath)}";
        File.Copy(sourcePath, Path.Combine(AppData.SoundsDir, fileName), true);

        var item = new SoundItem
        {
            Name = name,
            File = fileName,
            Duration = durationSeconds,
            Volume = 0.9
        };
        Items.Add(item);
        return item;
    }

    public SoundItem RegisterDownloaded(string pathInSoundsDir, double durationSeconds, string displayName, string url)
    {
        var name = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(pathInSoundsDir)
            : displayName;

        var item = new SoundItem
        {
            Name = name,
            File = Path.GetFileName(pathInSoundsDir),
            Duration = durationSeconds,
            Volume = 0.9,
            Url = url
        };
        Items.Add(item);
        return item;
    }

    public void Remove(SoundItem item)
    {
        Items.Remove(item);
        try
        {
            File.Delete(Path.Combine(AppData.SoundsDir, item.File));
        }
        catch
        {
            // file may be locked; the library entry is removed anyway
        }
    }
}
