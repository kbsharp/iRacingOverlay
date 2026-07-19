using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// The projected-iRating chip shared by the relative and standings session
/// strips: an arrow, the points at stake, and nothing at all when there is no
/// meaningful number to show.
///
/// Owns an <see cref="IRatingTracker"/>, so each widget tracks its own copy -
/// the tracker is cheap, and sharing one would couple the two windows' update
/// order together for no gain.
/// </summary>
public sealed class IRatingChipViewModel : ObservableObject
{
    private readonly IRatingTracker _tracker = new();

    private bool _hasValue;
    private bool _isFinal;
    private string _deltaText = string.Empty;
    private string _arrow = string.Empty;
    private RatingTrend _trend = RatingTrend.Flat;
    private int _projected;

    /// <summary>False outside a race, and before the projection means anything.</summary>
    public bool HasValue
    {
        get => _hasValue;
        private set => SetProperty(ref _hasValue, value);
    }

    /// <summary>True once the player has taken the flag and the value is captured.</summary>
    public bool IsFinal
    {
        get => _isFinal;
        private set => SetProperty(ref _isFinal, value);
    }

    /// <summary>The unsigned points at stake; <see cref="Arrow"/> carries the sign.</summary>
    public string DeltaText
    {
        get => _deltaText;
        private set => SetProperty(ref _deltaText, value);
    }

    public string Arrow
    {
        get => _arrow;
        private set => SetProperty(ref _arrow, value);
    }

    /// <summary>Drives the chip's colour: gain, loss, or level.</summary>
    public RatingTrend Trend
    {
        get => _trend;
        private set => SetProperty(ref _trend, value);
    }

    /// <summary>Where the player's iRating lands if the race ends as it stands.</summary>
    public int Projected
    {
        get => _projected;
        private set => SetProperty(ref _projected, value);
    }

    public void Update(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        var projection = _tracker.Update(snapshot, metadata);

        HasValue = projection.HasValue;
        IsFinal = projection.State == IRatingProjectionState.Final;

        if (!projection.HasValue)
        {
            return;
        }

        Trend = RatingFormat.ClassifyTrend(projection.Delta);
        DeltaText = RatingFormat.DeltaMagnitude(projection.Delta);
        Projected = projection.Projected;

        Arrow = Trend switch
        {
            RatingTrend.Up => "▲",
            RatingTrend.Down => "▼",
            _ => "–",
        };
    }
}
