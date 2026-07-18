using System.Collections.ObjectModel;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Radar;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// A top-down proximity radar. iRacing gives no position or heading for other
/// cars - only each car's lap fraction - so the widget learns the track's shape
/// from the player's own heading around the lap (<see cref="TrackMap"/>) and uses
/// it to place the field in the player's local frame, angles and all
/// (<see cref="RadarCalculator"/>). It hides itself entirely when no one is near,
/// and falls back to iRacing's coarse left/right spotter signal for the first lap
/// before the track is mapped.
/// </summary>
public sealed class RadarViewModel : OverlayViewModelBase
{
    // Don't learn the track while parked, spun or crawling - a stationary heading
    // would poison a bucket. Any real racing speed clears this easily.
    private const double MinMappingSpeedMps = 3.0;

    private double _rangeMeters = new WidgetTuning().RadarRangeMeters;

    private readonly TrackMap _trackMap = new();
    private SessionMetadata? _metadata;

    private bool _isActive;
    private bool _mapReady;
    private bool _hasCarLeft;
    private bool _hasCarRight;
    private bool _shouldShow;

    public RadarViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    /// <summary>Cars currently close enough to draw, updated in place each frame.</summary>
    public ObservableCollection<RadarBlipViewModel> Blips { get; } = [];

    /// <summary>False before the sim starts reporting (e.g. not yet on track).</summary>
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(IsWaitingForData));
                OnPropertyChanged(nameof(ShowFallback));
            }
        }
    }

    /// <summary>True once the track shape is learned and the positional radar is live.</summary>
    public bool MapReady
    {
        get => _mapReady;
        private set
        {
            if (SetProperty(ref _mapReady, value))
            {
                OnPropertyChanged(nameof(ShowRadar));
                OnPropertyChanged(nameof(ShowFallback));
            }
        }
    }

    /// <summary>Shown pre-session so the (otherwise auto-hiding) widget can be positioned.</summary>
    public bool IsWaitingForData => !IsActive;

    /// <summary>The positional canvas is drawn once the track is mapped and cars are near.</summary>
    public bool ShowRadar => MapReady && Blips.Count > 0;

    /// <summary>The coarse spotter zones stand in for the first lap, before the map is ready.</summary>
    public bool ShowFallback => IsActive && !MapReady && (HasCarLeft || HasCarRight);

    /// <summary>Whether the whole widget is visible - it disappears when nobody's near.</summary>
    public bool ShouldShow
    {
        get => _shouldShow;
        private set => SetProperty(ref _shouldShow, value);
    }

    public bool HasCarLeft
    {
        get => _hasCarLeft;
        private set
        {
            if (SetProperty(ref _hasCarLeft, value))
            {
                OnPropertyChanged(nameof(ShowFallback));
            }
        }
    }

    public bool HasCarRight
    {
        get => _hasCarRight;
        private set
        {
            if (SetProperty(ref _hasCarRight, value))
            {
                OnPropertyChanged(nameof(ShowFallback));
            }
        }
    }

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public override void ApplySettings(OverlaySettings settings)
        => _rangeMeters = settings.Tuning.RadarRangeMeters;

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var state = snapshot.CarLeftRight;
        IsActive = RadarFormat.IsActive(state);
        HasCarLeft = RadarFormat.HasCarLeft(state);
        HasCarRight = RadarFormat.HasCarRight(state);

        // Learn the track from the player's own heading as they lap.
        if (snapshot.IsOnTrack
            && snapshot.SpeedMetersPerSecond > MinMappingSpeedMps
            && TryPlayerLapDistPct(snapshot, out var playerPct))
        {
            _trackMap.Sample(playerPct, snapshot.PlayerYawRad);
        }

        var trackLength = _metadata?.TrackLengthMeters ?? 0;
        var result = RadarCalculator.Compute(snapshot, _metadata, _trackMap, trackLength, _rangeMeters);

        MapReady = result.MapReady;
        UpdateBlips(result.Blips);

        OnPropertyChanged(nameof(ShowRadar));
        ShouldShow = ShowRadar || ShowFallback || IsWaitingForData;
    }

    /// <summary>Refresh the blip slots in place, only resizing the collection when the
    /// number of nearby cars changes - keeps the canvas churn-free between frames.
    /// Ordered by car index so a given car keeps its slot while it stays in range.</summary>
    private void UpdateBlips(IReadOnlyList<RadarBlip> blips)
    {
        var ordered = blips.OrderBy(b => b.CarIdx).ToList();

        if (Blips.Count != ordered.Count)
        {
            if (Blips.Count < ordered.Count)
            {
                while (Blips.Count < ordered.Count)
                {
                    Blips.Add(new RadarBlipViewModel());
                }
            }
            else
            {
                while (Blips.Count > ordered.Count)
                {
                    Blips.RemoveAt(Blips.Count - 1);
                }
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            Blips[i].Update(ordered[i]);
        }
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
