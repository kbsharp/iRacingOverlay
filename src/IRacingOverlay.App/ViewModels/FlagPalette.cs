using IRacingOverlay.Core.Telemetry;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Chip colours for the flag readout. Unlike the rest of the palette these are
/// dictated by the real world - a yellow flag has to be yellow - so they sit
/// outside the panel's hue-per-meaning scheme deliberately.
/// </summary>
internal static class FlagPalette
{
    /// <summary>Fill, border and text brushes for a flag state.</summary>
    public static (Brush Background, Brush Border, Brush Foreground) Resolve(SessionFlagState state) =>
        state switch
        {
            SessionFlagState.Green => Chip("#33D689"),
            SessionFlagState.Yellow => Chip("#FFD23D"),
            SessionFlagState.Blue => Chip("#4DA3FF"),
            SessionFlagState.Red => Chip("#FF4D4D"),
            SessionFlagState.Meatball => Chip("#FF9436"),
            // White, chequered and the personal black flags all read as
            // white-on-dark; the label carries the distinction.
            SessionFlagState.White or SessionFlagState.Checkered => Chip("#FBFCFF"),
            SessionFlagState.Black or SessionFlagState.Disqualified => Chip("#E0E4EC"),
            _ => Chip("#98A9C6"),
        };

    /// <summary>Builds a tinted chip from one accent colour: a low-alpha wash, a
    /// stronger border, and the full-strength colour for the text - the same
    /// recipe the pit and iRating chips use.</summary>
    private static (Brush, Brush, Brush) Chip(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);

        return (
            Freeze(Color.FromArgb(0x3D, color.R, color.G, color.B)),
            Freeze(Color.FromArgb(0x8A, color.R, color.G, color.B)),
            Freeze(color));
    }

    private static Brush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
