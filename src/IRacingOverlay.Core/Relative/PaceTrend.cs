namespace IRacingOverlay.Core.Relative;

/// <summary>Which way a battle is going, from the player's point of view.</summary>
public enum PaceTrendDirection
{
    /// <summary>Not enough clean history yet to say anything.</summary>
    Unknown,

    /// <summary>The gap is shrinking - you are catching them, or they are catching you.</summary>
    Closing,

    /// <summary>The gap is stable within the noise floor.</summary>
    Holding,

    /// <summary>The gap is growing.</summary>
    Pulling,
}

/// <summary>
/// The forecast for one battle: how fast the gap is moving and, when it is
/// closing, when it actually runs out.
///
/// <see cref="RateSecondsPerLap"/> is signed so that <b>positive means closing</b>
/// - the gap is shrinking by that many seconds each lap. That is the direction a
/// driver cares about; a negative rate is someone escaping.
/// </summary>
public readonly record struct PaceTrend(
    PaceTrendDirection Direction,
    double RateSecondsPerLap,
    double? LapsToContact,
    bool? ArrivesBeforeFlag)
{
    /// <summary>No forecast: too few samples, a car in the pits, or a fresh session.</summary>
    public static readonly PaceTrend None = new(PaceTrendDirection.Unknown, 0, null, null);

    /// <summary>True when there is a rate worth showing.</summary>
    public bool HasRate => Direction is PaceTrendDirection.Closing or PaceTrendDirection.Pulling;
}
