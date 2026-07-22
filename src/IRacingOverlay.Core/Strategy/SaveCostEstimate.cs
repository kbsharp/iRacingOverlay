namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// What fuel saving costs this driver, in this car, today - learned from their
/// own laps by <see cref="SaveCostTracker"/>.
/// </summary>
/// <param name="SecondsPerLiter">Lap time given up for each litre per lap saved.
/// Positive: burning less makes the lap slower.</param>
/// <param name="LowestObservedLitersPerLap">The leanest lap actually driven. The
/// estimate describes the range the driver has been in, so this is carried to say
/// where that range ends.</param>
/// <param name="ObservedSpreadLitersPerLap">Distance between the leanest and
/// thirstiest laps in the window - how much of a range the slope was fitted over,
/// and therefore how far beyond it is still reasonable to read.</param>
public readonly record struct SaveCostEstimate(
    double SecondsPerLiter,
    double LowestObservedLitersPerLap,
    double ObservedSpreadLitersPerLap)
{
    /// <summary>Not enough laps, not enough variation between them, or a fit that
    /// came out backwards - all of which mean "no answer" rather than a guess.</summary>
    public static SaveCostEstimate None { get; } = new(0, 0, 0);

    public bool IsKnown => SecondsPerLiter > 0;
}
