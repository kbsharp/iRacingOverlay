using System.Globalization;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Display strings for the push-or-save tradeoff.
///
/// The headline is the *total* cost of saving, not the per-lap rate, because the
/// total is the figure that compares with a pit stop - and a bare rate with
/// nothing to compare it to is exactly what switched the catch/defend column off.
/// The rate still appears, on the working line underneath, where it says what it
/// is a rate of and over how many laps.
/// </summary>
public static class FuelSaveFormat
{
    /// <summary>The whole bill for saving to the flag, e.g. "5.6s" or "34s".</summary>
    public static string TotalCost(in FuelSavePlan plan) =>
        plan.HasPlan ? Seconds(plan.TotalCostSeconds) + "s" : TelemetryFormat.Placeholder;

    /// <summary>
    /// What the alternative costs, e.g. "vs 29s to pit" - empty until enough stops
    /// have been seen to know. The strip still prices saving in that case; it just
    /// has nothing honest to weigh it against yet.
    /// </summary>
    public static string Alternative(in FuelSavePlan plan) =>
        plan.HasPlan && plan.PitLossSeconds is { } loss
            ? "vs " + loss.ToString("0", CultureInfo.InvariantCulture) + "s to pit"
            : string.Empty;

    /// <summary>
    /// The working behind the headline, e.g. "0.4s/lap slower for 14 laps" - the
    /// part a driver can check on the next lap.
    /// </summary>
    public static string Working(in FuelSavePlan plan)
    {
        if (!plan.HasPlan)
        {
            return string.Empty;
        }

        var laps = plan.LapsRemaining.ToString(CultureInfo.InvariantCulture);
        var lapWord = plan.LapsRemaining == 1 ? " lap" : " laps";

        return plan.CostPerLapSeconds.ToString("0.0", CultureInfo.InvariantCulture)
            + "s/lap slower for " + laps + lapWord;
    }

    /// <summary>
    /// A tenth below ten seconds, whole seconds above it. Both sides of this
    /// decision are estimates; quoting "34.2s" against a pit loss shown as "29s"
    /// would claim a precision neither number has.
    /// </summary>
    private static string Seconds(double value) =>
        value < 10
            ? value.ToString("0.0", CultureInfo.InvariantCulture)
            : value.ToString("0", CultureInfo.InvariantCulture);
}
