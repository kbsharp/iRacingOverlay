using System.Globalization;
using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Display strings for the next-stop actions on the fuel widget.
/// </summary>
public static class FuelStrategyFormat
{
    /// <summary>
    /// The stops-still-to-come suffix beside the "add" figure, e.g. "+1 stop" or
    /// "+2 stops"; empty when the next stop (or none) covers the race. It exists
    /// because a capped "add" figure would otherwise silently understate the job -
    /// "fill it" reads as "you're done" unless it says a stop is still owed.
    /// </summary>
    public static string AdditionalStops(in FuelStrategy strategy)
    {
        var stops = strategy.AdditionalStops;
        if (stops <= 0)
        {
            return string.Empty;
        }

        var word = stops == 1 ? " stop" : " stops";
        return "+" + stops.ToString(CultureInfo.InvariantCulture) + word;
    }
}
