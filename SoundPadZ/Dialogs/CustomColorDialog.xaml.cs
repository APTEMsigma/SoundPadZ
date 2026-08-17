using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SoundPadZ.Dialogs;

public partial class CustomColorDialog : Window
{
    private static readonly string[] PresetSwatches =
    {
        "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3",
        "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
        "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548", "#607D8B"
    };

    private bool _syncing;
    public string SelectedHex { get; private set; } = "#90CAF9";

    public CustomColorDialog(string initialHex)
    {
        InitializeComponent();
        BuildSwatches();
        SetCurrentColor(initialHex);
    }

    private void BuildSwatches()
    {
        SwatchesPanel.Children.Clear();
        foreach (var hex in PresetSwatches)
        {
            var border = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Tag = hex
            };
            border.MouseLeftButtonDown += (_, _) =>
            {
                SetCurrentColor(hex);
            };
            SwatchesPanel.Children.Add(border);
        }
    }

    public void SetCurrentColor(string hex)
    {
        if (TryParseColor(hex, out var color))
        {
            _syncing = true;
            SelectedHex = hex.ToUpperInvariant();
            if (!SelectedHex.StartsWith('#')) SelectedHex = "#" + SelectedHex;

            TbHex.Text = SelectedHex;
            SlR.Value = color.R;
            SlG.Value = color.G;
            SlB.Value = color.B;

            UpdatePreview(color);
            _syncing = false;
        }
    }

    private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing) return;
        _syncing = true;

        var r = (byte)Math.Clamp(SlR.Value, 0, 255);
        var g = (byte)Math.Clamp(SlG.Value, 0, 255);
        var b = (byte)Math.Clamp(SlB.Value, 0, 255);
        var color = Color.FromRgb(r, g, b);

        SelectedHex = $"#{r:X2}{g:X2}{b:X2}";
        TbHex.Text = SelectedHex;
        UpdatePreview(color);

        _syncing = false;
    }

    private void TbHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        var text = TbHex.Text.Trim();
        if (TryParseColor(text, out var color))
        {
            _syncing = true;
            SelectedHex = text.StartsWith('#') ? text.ToUpperInvariant() : "#" + text.ToUpperInvariant();
            SlR.Value = color.R;
            SlG.Value = color.G;
            SlB.Value = color.B;
            UpdatePreview(color);
            _syncing = false;
        }
    }

    private void UpdatePreview(Color color)
    {
        var brush = new SolidColorBrush(color);
        PreviewBorder.Background = brush;
        TxtHexBadge.Text = SelectedHex;

        // Luminance-based contrast for preview text
        var lum = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        TxtPreview.Foreground = lum > 140 ? Brushes.Black : Brushes.White;
    }

    private static bool TryParseColor(string hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            var raw = hex.Trim();
            if (!raw.StartsWith('#')) raw = "#" + raw;
            if (raw.Length != 7 && raw.Length != 9) return false;
            var obj = ColorConverter.ConvertFromString(raw);
            if (obj is Color c)
            {
                color = c;
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
