using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One car on the radar canvas. Holds the already-mapped canvas position (top-left
/// for <c>Canvas.Left</c>/<c>Canvas.Top</c>) and on-screen rotation, so the XAML
/// binds straight through with no converters. Updated in place each frame (no
/// per-frame collection churn), mirroring the standings/relative row view models.
/// </summary>
public sealed class RadarBlipViewModel : ObservableObject
{
    private double _canvasLeft;
    private double _canvasTop;
    private double _angleDegrees;
    private string _number = string.Empty;
    private double _opacity = 1.0;

    /// <summary>How solid a car with no known side is drawn - present, but plainly
    /// less certain than the marks around it. See <see cref="RadarBlip.LateralUnresolved"/>.</summary>
    public const double UnresolvedOpacity = 0.45;

    public double CanvasLeft
    {
        get => _canvasLeft;
        private set => SetProperty(ref _canvasLeft, value);
    }

    public double CanvasTop
    {
        get => _canvasTop;
        private set => SetProperty(ref _canvasTop, value);
    }

    public double AngleDegrees
    {
        get => _angleDegrees;
        private set => SetProperty(ref _angleDegrees, value);
    }

    public string Number
    {
        get => _number;
        private set => SetProperty(ref _number, value);
    }

    /// <summary>Full strength for a placed car, faded for one whose side is unknown.</summary>
    public double Opacity
    {
        get => _opacity;
        private set => SetProperty(ref _opacity, value);
    }

    /// <param name="blip">The car's position in the player's local frame, in metres.</param>
    /// <param name="pixelsPerMeter">The scale for the range currently in view - see
    /// <see cref="RadarLayout.ScaleFor"/>. Passed in rather than read from a constant
    /// so the canvas always shows exactly the range the driver asked for.</param>
    public void Update(RadarBlip blip, double pixelsPerMeter)
    {
        // Metres -> canvas pixels: forward (+) is up the screen, right (+) is right.
        var pointX = RadarLayout.CenterX + (blip.RightMeters * pixelsPerMeter);
        var pointY = RadarLayout.CenterY - (blip.ForwardMeters * pixelsPerMeter);

        CanvasLeft = pointX - (RadarLayout.BlipWidth / 2);
        CanvasTop = pointY - (RadarLayout.BlipHeight / 2);

        // The geometry's angle is anticlockwise (world); WPF's RotateTransform is
        // clockwise because the canvas Y axis points down, so negate.
        AngleDegrees = -blip.RelativeAngleRad * 180.0 / System.Math.PI;
        Number = blip.CarNumber;
        Opacity = blip.LateralUnresolved ? UnresolvedOpacity : 1.0;
    }
}
