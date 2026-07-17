using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Radar;
using Brush = System.Windows.Media.Brush;

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
    private Brush _fill = System.Windows.Media.Brushes.Gray;

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

    public Brush Fill
    {
        get => _fill;
        private set => SetProperty(ref _fill, value);
    }

    public void Update(RadarBlip blip)
    {
        // Metres -> canvas pixels: forward (+) is up the screen, right (+) is right.
        var pointX = RadarLayout.CenterX + blip.RightMeters * RadarLayout.PixelsPerMeter;
        var pointY = RadarLayout.CenterY - blip.ForwardMeters * RadarLayout.PixelsPerMeter;

        CanvasLeft = pointX - RadarLayout.BlipWidth / 2;
        CanvasTop = pointY - RadarLayout.BlipHeight / 2;

        // The geometry's angle is anticlockwise (world); WPF's RotateTransform is
        // clockwise because the canvas Y axis points down, so negate.
        AngleDegrees = -blip.RelativeAngleRad * 180.0 / System.Math.PI;
        Number = blip.CarNumber;
        Fill = ClassColorBrush.Resolve(blip.ClassColorHex);
    }
}
