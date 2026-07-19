namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// The tank-gauge bar: how full the tank is, and where the fuel needed to
/// finish falls on the same scale.
/// </summary>
/// <param name="HasGauge">False when tank capacity is unknown, so the bar is
/// hidden rather than drawn against a guessed scale.</param>
/// <param name="FillFraction">Current fuel as a fraction of tank capacity, 0-1.</param>
/// <param name="ShowTick">False until fuel-to-finish can be computed.</param>
/// <param name="TickFraction">Fuel-to-finish on the same 0-1 scale.</param>
/// <param name="ClearsTick">True when the level is at or above the tick - the
/// bar's green/red state, and the same question the margin badge answers.</param>
public readonly record struct FuelGauge(
    bool HasGauge,
    double FillFraction,
    bool ShowTick,
    double TickFraction,
    bool ClearsTick);

/// <summary>
/// Turns fuel litres into the tank gauge's two positions. A quantity with a
/// natural zero and a natural maximum is read faster as a bar than as digits -
/// this is the only place in the app where that's true, which is why the fuel
/// widget gets an instrument and the timing widgets don't.
/// </summary>
public static class FuelGaugeCalculator
{
    public static FuelGauge Compute(
        double fuelLiters,
        double tankCapacityLiters,
        double? fuelToFinishLiters)
    {
        // No capacity means no scale. Drawing the bar against a guessed maximum
        // would be worse than not drawing it: a gauge that reads "half full"
        // when it isn't is actively misleading at a glance.
        if (!IsUsable(tankCapacityLiters) || tankCapacityLiters <= 0)
        {
            return new FuelGauge(false, 0, false, 0, true);
        }

        var fill = Fraction(fuelLiters, tankCapacityLiters);

        if (fuelToFinishLiters is not { } toFinish || !IsUsable(toFinish) || toFinish < 0)
        {
            return new FuelGauge(true, fill, false, 0, true);
        }

        // A to-finish figure past capacity pins the tick to the end of the bar:
        // the race can't be finished on this tankful, and the bar should say so
        // by showing the level short of a tick at the very top rather than
        // silently rescaling.
        var tick = Fraction(toFinish, tankCapacityLiters);

        return new FuelGauge(true, fill, true, tick, fuelLiters >= toFinish);
    }

    private static bool IsUsable(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Fraction(double value, double capacity)
    {
        if (!IsUsable(value) || value <= 0)
        {
            return 0;
        }

        return Math.Clamp(value / capacity, 0, 1);
    }
}
