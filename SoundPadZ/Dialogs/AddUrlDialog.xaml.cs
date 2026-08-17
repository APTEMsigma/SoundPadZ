using System.IO;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;
using SoundPadZ.Services;

namespace SoundPadZ;

public partial class AddUrlDialog : Window
{
    public string? ResultPath { get; private set; }
    public double ResultDuration { get; private set; }
    public string ResultName { get; private set; } = "";
    public string SourceUrl { get; private set; } = "";

    private bool _nameTouched;

    public AddUrlDialog()
    {
        InitializeComponent();
    }

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
    {
        _nameTouched = !string.IsNullOrWhiteSpace(TxtName.Text);
    }

    private void TxtUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_nameTouched)
        {
            return;
        }
        try
        {
            var uri = new Uri(TxtUrl.Text.Trim());
            var fileName = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
            TxtName.Text = Path.GetFileNameWithoutExtension(fileName);
        }
        catch
        {
            // not a complete URL yet; skip auto-fill
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtUrl.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            TxtStatus.Text = Loc.Get("ErrDownload");
            return;
        }

        BtnDownload.IsEnabled = false;
        Pb.Visibility = Visibility.Visible;
        TxtStatus.Text = Loc.Get("Downloading");

        try
        {
            var path = await Downloader.DownloadAsync(url, AppData.SoundsDir);
            var duration = await Task.Run(() =>
            {
                using var reader = new AudioFileReader(path);
                return reader.TotalTime.TotalSeconds;
            });

            ResultPath = path;
            ResultDuration = duration;
            SourceUrl = url;
            ResultName = string.IsNullOrWhiteSpace(TxtName.Text)
                ? Path.GetFileNameWithoutExtension(path)
                : TxtName.Text.Trim();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            var message = ex is Downloader.UnknownFormatException
                ? Loc.Get("ErrFormat")
                : Loc.Get("ErrDownload");
            TxtStatus.Text = message + "\n" + ex.Message;
            Pb.Visibility = Visibility.Collapsed;
            BtnDownload.IsEnabled = true;
        }
    }
}
