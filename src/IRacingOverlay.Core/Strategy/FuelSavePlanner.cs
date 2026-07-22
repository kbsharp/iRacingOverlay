using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// Prices the two ways out of being short on fuel.
///
/// The fuel widget already computes the burn that would just reach the finish on
/// what is in the tank (<see cref="FuelStrategy.SaveTargetLitersPerLap"/>). What
/// it never said is what driving to that number costs - so a driver could see the
/// target and still have no idea whether it beat simply stopping. This puts both
/// on the same scale: the lap time given up between here and the flag, against
/// the seconds a stop takes (<see cref="PitLossTracker"/>).
///
/// Both figures are measured - one off the driver's own laps, one off the field's
/// stops - and both are in seconds, which is the whole point. No verdict is
/// returned. Two numbers in the same unit are the sentence; a recommendation
/// would only hide which of them was doing the work.
/// </summary>
public static class FuelSavePlanner
{
    /// <summary>
    /// Prices the choice, or returns <see cref="FuelSavePlan.None"/> when there
    /// isn't one to price.
    /// </summary>
    /// <param name="strategy">The fuel picture - supplies the save target, the laps
    /// left, and whether saving is needed at all.</param>
    /// <param name="averageLitersPerLap">The current burn, which is what the target
    /// is a reduction from.</param>
    /// <param name="cost">What saving costs this driver, learned from their laps.</param>
    /// <param name="pitLossSeconds">The alternative, when it is known.</param>
    public static FuelSavePlan Compute(
        in FuelStrategy strategy,
        double? averageLitersPerLap,
        in SaveCostEstimate cost,
        double? pitLossSeconds)
    {
        // Making it on what's in the tank is not a decision. The strip appears
        // only while there is one.
        if (strategy.WillFinish || !cost.IsKnown)
        {
            return FuelSavePlan.None;
        }

        if (strategy.SaveTargetLitersPerLap is not { } target
            || strategy.RaceLapsRemaining is not { } laps
            || averageLitersPerLap is not { } burn)
        {
            return FuelSavePlan.None;
        }

        var saving = burn - target;
        if (saving <= 0 || laps < 1)
        {
            return FuelSavePlan.None;
        }

        // The slope describes the laps the driver has actually driven. Reading it
        // one whole observed range past the leanest of them is already generous;
        // beyond that the answer would be about laps nobody has driven, which is
        // how a measured number turns into a modelled one.
        if (target < cost.LowestObservedLitersPerLap - cost.ObservedSpreadLitersPerLap)
        {
            return FuelSavePlan.None;
        }

        var perLap = cost.SecondsPerLiter * saving;
        var wholeLaps = (int)laps;

        return new FuelSavePlan(
            CostPerLapSeconds: perLap,
            LapsRemaining: wholeLaps,
            TotalCostSeconds: perLap * wholeLaps,
            PitLossSeconds: pitLossSeconds is > 0 ? pitLossSeconds : null);
    }
}
