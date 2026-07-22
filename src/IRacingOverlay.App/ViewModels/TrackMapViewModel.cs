using System.Collections.ObjectModel;
using System.Globalization;
using IRacingOverlay.Core.Map;
using IRacingOverlay.Core.Radar;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;
using Point = System.Windows.Point;
using PointCollection = System.Windows.Media.PointCollection;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// The circuit, drawn from the player's own driving, with the whole field placed
/// on it.
///
/// Where the radar answers "who is beside me right now", this answers "where is
/// everyone" - where the pack has strung out, which way a rival went, how much
/// clear track is ahead. It shares the radar's trick and needs no track database:
/// the shape is learned live (<see cref="TrackMap"/>) and walked into an outline
/// (<see cref="TrackOutline"/>), so it is never missing a circuit and never stale
/// after a resurface. The cost is the first lap, during which the widget says so
/// rather than drawing half a track.
/// </summary>
public sealed class TrackMapViewModel : OverlayViewModelBase
{
    /// <summary>Same gate the radar uses: a stationary or spun car's heading would
    /// poison a bucket, and any real racing speed clears this easily.</summary>
    private const double MinMappingSpeedMps = 3.0;

    /// <summary>How much more of the lap must be learned before the outline is
    /// re-walked. The shape only improves as the unmapped stretches fill in, and
    /// re-walking is a few hundred trig calls plus a new polyline - worth doing as
    /// the map completes, not worth doing every frame for a bucket at a time.</summary>
    private const double RedrawCoverageStep = 0.05;

    /// <summary>Stands in for "no circuit yet". Frozen, like every collection this
    /// view model publishes: a <see cref="PointCollection"/> is a DependencyObject,
    /// so an unfrozen one belongs to the thread that made it - and these are made
    /// on whichever thread the telemetry frame arrived on.</summary>
    private static readonly PointCollection NoOutline = CreateFrozen();

    private readonly TrackMap _trackMap = new();

    private SessionMetadata? _metadata;
    private double _trackLengthMeters;
    private TrackOutline? _outline;
    private double _drawnCoverage;

    private PointCollection _outlinePoints = NoOutline;
    private bool _isActive;
    private string _mappingText = string.Empty;
    private Point _startLineFrom;
    private Point _startLineTo;

    public TrackMapViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    /// <summary>The field, updated in place each frame; the player is last, so their
    /// mark draws on top of whoever they're running with.</summary>
    public ObservableCollection<TrackMapCarViewModel> Cars { get; } = [];

    /// <summary>The circuit outline in canvas pixels, ready for a Polyline.</summary>
    public PointCollection OutlinePoints
    {
        get => _outlinePoints;
        private set => SetProperty(ref _outlinePoints, value);
    }

    /// <summary>True once there is a circuit to draw - roughly one clean lap in.</summary>
    public bool HasOutline => _outline is not null;

    /// <summary>False before the sim starts reporting (e.g. not yet on track).</summary>
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(IsMapping));
            }
        }
    }

    /// <summary>Whether the widget is still learning the circuit. Shown rather than
    /// hidden: an empty panel reads as broken, where "MAPPING 60%" says what it is
    /// waiting for and that it's making progress.</summary>
    public bool IsMapping => !HasOutline;

    /// <summary>How much of the lap has been learned, as a percentage.</summary>
    public string MappingText
    {
        get => _mappingText;
        private set => SetProperty(ref _mappingText, value);
    }

    /// <summary>Ends of the start/finish tick, drawn across the track at the line -
    /// without it a closed loop has no anchor, and which way round it goes is
    /// anyone's guess.</summary>
    public Point StartLineFrom
    {
        get => _startLineFrom;
        private set => SetProperty(ref _startLineFrom, value);
    }

    public Point StartLineTo
    {
        get => _startLineTo;
        private set => SetProperty(ref _startLineTo, value);
    }

    public override void ApplySessionMetadata(SessionMetadata metadata)
    {
        _metadata = metadata;

        // A different track length means a different track: the buckets are keyed
        // by lap fraction, so last circuit's headings would otherwise keep being
        // served for this one and the map would draw a track nobody is on.
        if (Math.Abs(metadata.TrackLengthMeters - _trackLengthMeters) > 1.0)
        {
            _trackLengthMeters = metadata.TrackLengthMeters;
            _trackMap.Reset();
            ClearOutline();
        }
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        IsActive = snapshot.IsOnTrack || snapshot.Cars.Count > 0;

        if (snapshot.IsOnTrack
            && snapshot.SpeedMetersPerSecond > MinMappingSpeedMps
            && TryPlayerLapDistPct(snapshot, out var playerPct))
        {
            _trackMap.Sample(playerPct, snapshot.PlayerYawRad);
        }

        RefreshOutline();

        if (_outline is null)
        {
            MappingText = (_trackMap.Coverage * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            UpdateCars([]);
            return;
        }

        UpdateCars(TrackMapCalculator.Compute(snapshot, _metadata));
    }

    /// <summary>Re-walks the outline when the map has learned meaningfully more of
    /// the lap than the drawn shape was built from.</summary>
    private void RefreshOutline()
    {
        if (_outline is not null && _trackMap.Coverage < _drawnCoverage + RedrawCoverageStep)
        {
            return;
        }

        var outline = TrackOutline.Build(_trackMap, _trackLengthMeters);
        if (outline is null)
        {
            return;
        }

        var had = _outline is not null;
        _outline = outline;
        _drawnCoverage = outline.Coverage;

        var points = new PointCollection(outline.Points.Count);
        foreach (var point in outline.Points)
        {
            points.Add(new Point(TrackMapLayout.ToCanvas(point.X), TrackMapLayout.ToCanvas(point.Y)));
        }

        // Close the loop for the polyline: the outline's last point joins the first,
        // but a Polyline only draws the segments between the points it is given.
        points.Add(points[0]);
        points.Freeze();
        OutlinePoints = points;

        SetStartLine(outline);

        if (!had)
        {
            OnPropertyChanged(nameof(HasOutline));
            OnPropertyChanged(nameof(IsMapping));
        }
    }

    /// <summary>Places the start/finish tick across the track at the line, square to
    /// the direction of travel there.</summary>
    private void SetStartLine(TrackOutline outline)
    {
        var at = outline.At(0.0);
        var ahead = outline.At(0.004);

        var dx = ahead.X - at.X;
        var dy = ahead.Y - at.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0)
        {
            dx = 1;
            dy = 0;
            length = 1;
        }

        // Unit normal to the racing line: just proud of the tarmac each way, the
        // way a painted line runs to the edge of the track and no further.
        var half = TrackMapLayout.TrackThickness * 0.7;
        var nx = -dy / length * half;
        var ny = dx / length * half;

        var x = TrackMapLayout.ToCanvas(at.X);
        var y = TrackMapLayout.ToCanvas(at.Y);
        StartLineFrom = new Point(x - nx, y - ny);
        StartLineTo = new Point(x + nx, y + ny);
    }

    private void ClearOutline()
    {
        _outline = null;
        _drawnCoverage = 0;
        OutlinePoints = NoOutline;
        UpdateCars([]);
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(IsMapping));
    }

    /// <summary>Refreshes the car slots in place, only resizing the collection when
    /// the number of cars in the session changes - the canvas stays churn-free
    /// between frames.</summary>
    private void UpdateCars(IReadOnlyList<TrackMapCar> cars)
    {
        while (Cars.Count < cars.Count)
        {
            Cars.Add(new TrackMapCarViewModel());
        }

        while (Cars.Count > cars.Count)
        {
            Cars.RemoveAt(Cars.Count - 1);
        }

        if (_outline is null)
        {
            return;
        }

        for (var i = 0; i < cars.Count; i++)
        {
            Cars[i].Update(cars[i], _outline);
        }
    }

    private static PointCollection CreateFrozen()
    {
        var points = new PointCollection();
        points.Freeze();
        return points;
    }

    private static bool TryPlayerLapDistPct(TelemetrySnapshot snapshot, out double lapDistPct)
    {
        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == snapshot.PlayerCarIdx && car.Surface != CarTrackSurface.NotInWorld)
            {
                lapDistPct = car.LapDistPct;
                return true;
            }
        }

        lapDistPct = 0;
        return false;
    }
}
