using System.Diagnostics;
using System.Windows;

namespace SoundPadZ.Dialogs;

public partial class WelcomeDialog : Window
{
    public bool DontShowAgain => ChkDontShow.IsChecked == true;

    public WelcomeDialog()
    {
        InitializeComponent();
    }

    private void DownloadCable_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://vb-audio.com/Cable/",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore if browser cannot be launched
        }
    }

    private void GotIt_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
