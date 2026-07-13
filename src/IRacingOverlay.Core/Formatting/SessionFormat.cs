using System.Globalization;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Formatting;

/// <summary>Display formatting for session-level values.</summary>
public static class SessionFormat
{
    /// <summary>iRacing reports roughly a week of seconds when a session has no time limit.</summary>
    private const double UnlimitedTimeSeconds = 604000;

    /// <summary>Formats remaining session time, or null when unlimited/unknown.</summary>
    public static string? TimeRemaining(double seconds)
    {
        if (seconds < 0 || seconds >= UnlimitedTimeSeconds)
        {
            return null;
        }

        var time = TimeSpan.FromSeconds(seconds);

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    public static string IRating(int irating) => irating >= 1000
        ? (irating / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k"
        : irating.ToString(CultureInfo.InvariantCulture);

    /// <summary>Signed one-decimal delta, e.g. "+1.2" / "-0.8".</summary>
    public static string Delta(double seconds)
    {
        var magnitude = Math.Abs(seconds).ToString("0.0", CultureInfo.InvariantCulture);
        return (seconds < 0 ? "-" : "+") + magnitude;
    }

    public static string Wetness(TrackWetness wetness) => wetness switch
    {
        TrackWetness.Unknown or TrackWetness.Dry => "DRY",
        TrackWetness.MostlyDry => "DRYING",
        TrackWetness.VeryLightlyWet or TrackWetness.LightlyWet => "DAMP",
        TrackWetness.ModeratelyWet => "WET",
        _ => "V.WET",
    };

    public static string Temperature(float celsius) =>
        MathF.Round(celsius).ToString(CultureInfo.InvariantCulture) + "°";
}
