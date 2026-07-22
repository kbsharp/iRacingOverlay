using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Map;
using Brush = System.Windows.Media.Brush;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One car on the track-map canvas. Holds the already-mapped canvas position
/// (top-left for <c>Canvas.Left</c>/<c>Canvas.Top</c>), so the XAML binds straight
/// through with no converters. Updated in place each frame, mirroring the radar's
/// blips and the list widgets' rows.
/// </summary>
public sealed class TrackMapCarViewModel : ObservableObject
{
    /// <summary>How solid a car in the pit lane is drawn. Its lap fraction runs
    /// along the lane, not the racing line, so the mark is a rougher statement
    /// than the ones around it and is drawn as one.</summary>
    public const double PitOpacity = 0.4;

    private double _canvasLeft;
    private double _canvasTop;
    private double _size = TrackMapLayout.CarSize;
    private double _opacity = 1.0;
    private bool _isPlayer;
    private Brush _fill = ClassColorBrush.Resolve(null);
    private string? _fillHex;

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

    /// <summary>Mark diameter - the player's is a size up (see <see cref="TrackMapLayout"/>).</summary>
    public double Size
    {
        get => _size;
        private set => SetProperty(ref _size, value);
    }

    public double Opacity
    {
        get => _opacity;
        private set => SetProperty(ref _opacity, value);
    }

    /// <summary>Drives the ring that marks your own car.</summary>
    public bool IsPlayer
    {
        get => _isPlayer;
        private set => SetProperty(ref _isPlayer, value);
    }

    /// <summary>iRacing's own class colour, so a car is the same hue here as it is
    /// on the standings and the relative.</summary>
    public Brush Fill
    {
        get => _fill;
        private set => SetProperty(ref _fill, value);
    }

    public void Update(TrackMapCar car, TrackOutline outline)
    {
        var point = outline.At(car.LapDistPct);
        var size = car.IsPlayer ? TrackMapLayout.PlayerCarSize : TrackMapLayout.CarSize;

        Size = size;
        CanvasLeft = TrackMapLayout.ToCanvas(point.X) - (size / 2);
        CanvasTop = TrackMapLayout.ToCanvas(point.Y) - (size / 2);
        IsPlayer = car.IsPlayer;
        Opacity = car.InPits ? PitOpacity : 1.0;

        // A car's class doesn't change while it's on track, but its position does
        // 30 times a second - so the brush is only rebuilt when the colour itself
        // moves (a slot taken over by a car from another class).
        if (_fillHex != car.ClassColorHex)
        {
            _fillHex = car.ClassColorHex;
            Fill = ClassColorBrush.Resolve(car.ClassColorHex);
        }
    }
}
