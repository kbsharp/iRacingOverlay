using System.Globalization;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Core.Weather;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Display formatting for the weather nowcast strip. Kept apart from
/// <see cref="SessionFormat.Wetness"/> on purpose: that one collapses the scale
/// into a compact three-way chip for the relative's header (and even reuses the
/// word "DRYING" as a state label), which would clash with the nowcast's own
/// WETTING/DRYING <i>trend</i> word. Here every wetness level keeps its full,
/// unambiguous name so "was Lightly Wet, now Very Wet" reads exactly.
/// </summary>
public static class WeatherFormat
{
    /// <summary>The full name of a wetness level, e.g. "Lightly Wet". Unknown
    /// renders as the placeholder rather than a made-up state.</summary>
    public static string WetnessLabel(TrackWetness wetness) => wetness switch
    {
        TrackWetness.Dry => "Dry",
        TrackWetness.MostlyDry => "Mostly Dry",
        TrackWetness.VeryLightlyWet => "Very Lightly Wet",
        TrackWetness.LightlyWet => "Lightly Wet",
        TrackWetness.ModeratelyWet => "Moderately Wet",
        TrackWetness.VeryWet => "Very Wet",
        TrackWetness.ExtremelyWet => "Extremely Wet",
        _ => TelemetryFormat.Placeholder,
    };

    /// <summary>The trend as the strip's headline word. Steady and insufficient
    /// have no headline - the strip is hidden in those states anyway.</summary>
    public static string TrendLabel(WeatherTrend trend) => trend switch
    {
        WeatherTrend.Wetting => "WETTING",
        WeatherTrend.Drying => "DRYING",
        _ => string.Empty,
    };

    /// <summary>
    /// How long ago the comparison reaches, rounded to whole minutes with a floor
    /// of one, e.g. "5 min". Used for "was Dry 5 min ago" - a plain, checkable
    /// referent rather than a raw seconds count.
    /// </summary>
    public static string MinutesAgo(double seconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero));
        return minutes.ToString(CultureInfo.InvariantCulture) + " min";
    }
}
