using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Turns a normalised "#RRGGBB" class colour (from the sim) into a frozen WPF
/// brush, with a neutral grey fallback. Shared by the relative and standings
/// rows so a car's class colour looks identical in both.
/// </summary>
internal static class ClassColorBrush
{
    public static Brush Resolve(string? hex)
    {
        if (hex is null)
        {
            return Brushes.Gray;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return Brushes.Gray;
        }
    }
}
