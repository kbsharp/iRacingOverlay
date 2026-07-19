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

    /// <summary>
    /// Parses one of the sim's session-info limit strings (<c>WeekendOptions:
    /// IncidentLimit</c>, <c>Session: SessionLaps</c>), which carry either a
    /// number or the word "unlimited". Returns null for unlimited/unparseable.
    /// </summary>
    public static int? ParseLimit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value > 0
                ? value
                : null;
    }

    /// <summary>
    /// Incident count against the session's limit, e.g. "4x/17x"; falls back to
    /// "4x" when the session is unlimited (most practice/open sessions).
    /// </summary>
    public static string Incidents(int count, int? limit)
    {
        var incidents = count.ToString(CultureInfo.InvariantCulture) + "x";

        return limit is { } max
            ? incidents + "/" + max.ToString(CultureInfo.InvariantCulture) + "x"
            : incidents;
    }

    /// <summary>
    /// How close the driver is to the incident limit, so the UI can warn before
    /// it matters rather than after. Unlimited sessions are always <c>Ok</c>.
    /// </summary>
    public static IncidentSeverity IncidentLevel(int count, int? limit)
    {
        if (limit is not { } max || max <= 0)
        {
            return IncidentSeverity.Ok;
        }

        var fraction = (double)count / max;

        return fraction switch
        {
            >= 0.9 => IncidentSeverity.Critical,
            >= 0.7 => IncidentSeverity.Warning,
            _ => IncidentSeverity.Ok,
        };
    }

    /// <summary>
    /// The player's lap against the race distance, e.g. "L12/25". Falls back to
    /// "L12" when the session is timed rather than lap-limited, and to an empty
    /// string before the player has completed anything (lap 0 in the pits).
    /// </summary>
    public static string LapCounter(int lap, int? totalLaps)
    {
        if (lap <= 0)
        {
            return string.Empty;
        }

        var current = "L" + lap.ToString(CultureInfo.InvariantCulture);

        // Clamp: the sim keeps counting past the total on the cool-down lap, and
        // "L26/25" reads as a bug rather than as a finished race.
        return totalLaps is { } total
            ? "L" + Math.Min(lap, total).ToString(CultureInfo.InvariantCulture)
                + "/" + total.ToString(CultureInfo.InvariantCulture)
            : current;
    }

    /// <summary>Looks up the display name for a session number, upper-cased,
    /// falling back to "SESSION" when unknown.</summary>
    public static string ResolveSessionType(IReadOnlyDictionary<int, string>? sessionTypesByNum, int sessionNum)
    {
        if (sessionTypesByNum is not null
            && sessionTypesByNum.TryGetValue(sessionNum, out var type)
            && type.Length > 0)
        {
            return type.ToUpperInvariant();
        }

        return "SESSION";
    }
}
