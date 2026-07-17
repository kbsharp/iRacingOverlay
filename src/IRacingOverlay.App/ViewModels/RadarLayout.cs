namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Fixed geometry of the radar canvas, shared by <see cref="RadarBlipViewModel"/>
/// (which maps metres to canvas pixels) and the XAML (via <c>x:Static</c>, so the
/// canvas and blip sizes can't drift out of sync). The player sits at the centre
/// facing up; forward is -Y, the player's right is +X.
/// </summary>
public static class RadarLayout
{
    public const double CanvasWidth = 150;
    public const double CanvasHeight = 240;

    public const double CenterX = CanvasWidth / 2;
    public const double CenterY = CanvasHeight / 2;

    /// <summary>Canvas pixels per real metre. Car icons are drawn at a fixed size
    /// (below), only their positions scale - the usual radar convention, so cars
    /// stay legible however close together they run.</summary>
    public const double PixelsPerMeter = 1.6;

    public const double BlipWidth = 12;
    public const double BlipHeight = 18;
}
