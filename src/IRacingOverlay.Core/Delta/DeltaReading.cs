namespace IRacingOverlay.Core.Delta;

/// <summary>
/// One frame of the delta bar: how far off the reference lap the driver is, how
/// to colour it, and how far the bar fills from its centre.
/// </summary>
/// <param name="Seconds">Signed - negative is faster than the reference.</param>
/// <param name="BarFraction">0-1 of the bar's half-width, clamped at
/// <see cref="DeltaCalculator.FullScaleSeconds"/>.</param>
public readonly record struct DeltaReading(
    DeltaState State,
    double Seconds,
    DeltaTone Tone,
    double BarFraction)
{
    /// <summary>Nothing to show.</summary>
    public static DeltaReading Empty { get; } = new(DeltaState.None, 0, DeltaTone.Neutral, 0);

    /// <summary>Whether there is a number worth drawing.</summary>
    public bool HasDelta => State is DeltaState.Live or DeltaState.LapComplete;
}
