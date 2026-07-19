using System.Globalization;
using IRacingOverlay.Core.Relative;

namespace IRacingOverlay.Core.Formatting;

/// <summary>What a pace trend means for the player, which is what colours it.</summary>
public enum PaceTrendTone
{
    /// <summary>Nothing to act on: holding, escaping, or a catch that won't land before the flag.</summary>
    Neutral,

    /// <summary>You are catching someone ahead, and you get there in time.</summary>
    Gain,

    /// <summary>Someone behind is catching you, and they get there in time.</summary>
    Threat,
}

/// <summary>
/// Display forms for <see cref="PaceTrend"/>. The arrow reports the gap's
/// direction (down = shrinking) and the colour reports what that means for the
/// player - two independent facts, so neither has to carry the other's job.
/// </summary>
public static class PaceTrendFormat
{
    /// <summary>Rounded down to a whole lap: "in 6 laps" that turns out to be 5 is a
    /// worse promise than one that turns out to be 7.</summary>
    private const double MinLapsShown = 1;

    /// <summary>Gap direction: down when it's shrinking, up when it's growing.</summary>
    public static string Arrow(PaceTrend trend) => trend.Direction switch
    {
        PaceTrendDirection.Closing => "▼",
        PaceTrendDirection.Pulling => "▲",
        _ => string.Empty,
    };

    /// <summary>Unsigned seconds per lap, e.g. "0.4"; empty when there is no rate.</summary>
    public static string Rate(PaceTrend trend) => trend.HasRate
        ? Math.Abs(trend.RateSecondsPerLap).ToString("0.0", CultureInfo.InvariantCulture)
        : string.Empty;

    /// <summary>
    /// Laps until the gap runs out, e.g. "6L". Empty unless the battle is
    /// closing <i>and</i> arrives before the flag - a countdown to a meeting
    /// that the session ends before is noise, not a forecast.
    /// </summary>
    public static string LapsToContact(PaceTrend trend)
    {
        if (trend.Direction != PaceTrendDirection.Closing
            || trend.LapsToContact is not { } laps
            || trend.ArrivesBeforeFlag == false)
        {
            return string.Empty;
        }

        var whole = Math.Max(MinLapsShown, Math.Floor(laps));
        return whole.ToString("0", CultureInfo.InvariantCulture) + "L";
    }

    /// <summary>
    /// The trend's meaning for the player. Only a closing battle that actually
    /// arrives earns a colour; everything else stays quiet, so the one row worth
    /// looking at is the one that lights up.
    /// </summary>
    public static PaceTrendTone Tone(PaceTrend trend, bool isAhead)
    {
        if (trend.Direction != PaceTrendDirection.Closing || trend.ArrivesBeforeFlag == false)
        {
            return PaceTrendTone.Neutral;
        }

        return isAhead ? PaceTrendTone.Gain : PaceTrendTone.Threat;
    }
}
