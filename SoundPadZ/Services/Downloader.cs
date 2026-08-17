using System.IO;
using System.Net.Http;

namespace SoundPadZ.Services;

public static class Downloader
{
    public sealed class UnknownFormatException : Exception
    {
        public UnknownFormatException(string message) : base(message) { }
    }

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) SoundPadZ/1.0");
        return client;
    }

    private static readonly HashSet<string> KnownExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".aiff", ".mp4", ".ogg", ".opus", ".webm"
        };

    public static async Task<string> DownloadAsync(string url, string destDir)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var uri = new Uri(url);
        var ext = Path.GetExtension(uri.AbsolutePath);
        if (!KnownExtensions.Contains(ext))
        {
            var mime = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            ext = mime switch
            {
                "audio/mpeg" or "audio/mp3" => ".mp3",
                "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
                "audio/mp4" or "video/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
                "audio/aac" => ".aac",
                "audio/flac" or "audio/x-flac" => ".flac",
                "audio/x-ms-wma" => ".wma",
                "audio/aiff" or "audio/x-aiff" => ".aiff",
                "audio/ogg" or "application/ogg" => ".ogg",
                "audio/webm" => ".webm",
                _ => throw new UnknownFormatException("Unknown audio type: " + (mime ?? "unspecified"))
            };
        }

        var name = Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(uri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "sound";
        }
        name = Sanitize(name);
        if (name.Length > 40)
        {
            name = name[..40];
        }

        var dest = Path.Combine(destDir, $"{name}_{Guid.NewGuid().ToString("N")[..6]}{ext}");
        await using (var stream = File.Create(dest))
        {
            await response.Content.CopyToAsync(stream);
        }
        return dest;
    }

    public static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }
        return value.Trim();
    }
}
