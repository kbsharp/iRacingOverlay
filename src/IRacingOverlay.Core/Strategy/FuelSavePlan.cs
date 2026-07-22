namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// The push-or-save decision, priced. Both sides are in seconds, so the driver
/// compares two numbers in the same unit rather than a rate against a position.
/// </summary>
/// <param name="CostPerLapSeconds">Lap time given up per lap by driving to the
/// save target instead of the current burn.</param>
/// <param name="LapsRemaining">Laps that cost is paid over.</param>
/// <param name="TotalCostSeconds">The whole bill for saving to the flag - the
/// figure that compares directly with a pit stop.</param>
/// <param name="PitLossSeconds">What stopping costs instead, when it has been
/// learned; null when no stops have been observed yet, in which case the plan
/// still prices saving but has nothing to price it against.</param>
public readonly record struct FuelSavePlan(
    double CostPerLapSeconds,
    int LapsRemaining,
    double TotalCostSeconds,
    double? PitLossSeconds)
{
    /// <summary>No decision to price: enough fuel already, no learned save cost,
    /// or a target further off the driver's observed laps than those laps can speak to.</summary>
    public static FuelSavePlan None { get; } = new(0, 0, 0, null);

    public bool HasPlan => CostPerLapSeconds > 0;
}
