namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// Turns current fuel, burn rate, and how much race is left into the strategic
/// numbers a driver acts on: fuel to the finish, the margin they'll finish
/// with, how much to add at the next stop, and the per-lap burn they'd need to
/// make it without stopping.
/// </summary>
public static class FuelStrategyCalculator
{
    /// <summary>iRacing reports this (0x7FFF) for laps-remaining in timed/unlimited races.</summary>
    public const int UnlimitedLaps = 32767;

    /// <summary>iRacing reports roughly a week of seconds when a session has no time limit.</summary>
    private const double UnlimitedTimeSeconds = 604000;

    /// <summary>
    /// Builds the strategy. <paramref name="safetyMarginLaps"/> is the buffer,
    /// in laps of fuel, folded into the "fuel to add" figure so a stop leaves
    /// headroom rather than finishing bone dry.
    /// </summary>
    public static FuelStrategy Compute(
        double currentFuelLiters,
        double? averageLitersPerLap,
        double? raceLapsRemaining,
        double safetyMarginLaps = 0.5)
    {
        if (averageLitersPerLap is not > 0.0 || raceLapsRemaining is not >= 0.0)
        {
            return FuelStrategy.Unknown;
        }

        var avg = averageLitersPerLap.Value;
        var laps = raceLapsRemaining.Value;

        var fuelToFinish = avg * laps;
        var marginLiters = currentFuelLiters - fuelToFinish;
        var marginLaps = marginLiters / avg;

        var bufferLiters = avg * safetyMarginLaps;
        var fuelToAdd = Math.Max(0.0, fuelToFinish + bufferLiters - currentFuelLiters);

        double? saveTarget = laps > 0 ? currentFuelLiters / laps : null;

        return new FuelStrategy(
            RaceLapsRemaining: laps,
            FuelToFinishLiters: fuelToFinish,
            MarginLaps: marginLaps,
            FuelToAddLiters: fuelToAdd,
            SaveTargetLitersPerLap: saveTarget,
            WillFinish: marginLiters >= 0);
    }

    /// <summary>
    /// Estimates whole laps left in the race. Prefers the sim's lap count for
    /// lap-limited races; for timed races, derives it from time remaining and
    /// average lap time (rounded up - the lap in progress must be completed).
    /// Returns null when neither is known.
    /// </summary>
    public static double? EstimateRaceLapsRemaining(
        int sessionLapsRemaining,
        double sessionTimeRemainingSeconds,
        double? averageLapTimeSeconds)
    {
        if (sessionLapsRemaining >= 0 && sessionLapsRemaining < UnlimitedLaps)
        {
            return sessionLapsRemaining;
        }

        if (averageLapTimeSeconds is > 0.0
            && sessionTimeRemainingSeconds >= 0
            && sessionTimeRemainingSeconds < UnlimitedTimeSeconds)
        {
            return Math.Ceiling(sessionTimeRemainingSeconds / averageLapTimeSeconds.Value);
        }

        return null;
    }
}
