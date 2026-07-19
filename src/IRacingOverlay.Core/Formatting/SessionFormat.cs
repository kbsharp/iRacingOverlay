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
    /// The session strip's two headline parts, split so they can be typeset
    /// separately: the session type ("RACE") as a quiet label, and the primary
    /// figure ("3:24" or "12 LAPS") as the headline. Joined into one string they
    /// could only ever share one size, which is what made the strip read as a
    /// row of equal-weight tokens.
    /// </summary>
    /// <param name="RemainingText">Empty when the session is unlimited and has
    /// no lap count either - the strip then shows the type alone.</param>
    public readonly record struct SessionHeader(string TypeText, string RemainingText);

    /// <summary>
    /// The session type reduced to the one word that identifies it.
    ///
    /// iRacing's session names carry a qualifier the driver does not need in the
    /// strip: "Open Qualify" and "Lone Qualify" are both, to the person in the
    /// car, QUALIFY - the distinction changes nothing they do. The full names are
    /// what put the session strip over its width budget: at 470px the relative
    /// cannot fit "OPEN QUALIFY" alongside the clock, lap counter, flag chip,
    /// projected-iRating chip and the whole right-hand telemetry group, and the
    /// alternatives to shortening are all worse - trimming renders a meaningless
    /// ".." stub, and hiding the label outright loses it even when there was room.
    ///
    /// Matching on a substring rather than an exact name deliberately: iRacing
    /// fields several variants ("Heat Race", "Offline Testing") and an unknown
    /// name passes through uppercased rather than being dropped.
    /// </summary>
    public static string ShortType(string sessionType)
    {
        if (string.IsNullOrWhiteSpace(sessionType))
        {
            return string.Empty;
        }

        var trimmed = sessionType.Trim();

        // Order matters: "Heat Race" must match RACE, and it contains neither
        // "practice" nor "qualif", so the race check can sit after them safely.
        if (trimmed.Contains("qualif", StringComparison.OrdinalIgnoreCase))
        {
            return "QUALIFY";
        }

        if (trimmed.Contains("practice", StringComparison.OrdinalIgnoreCase))
        {
            return "PRACTICE";
        }

        if (trimmed.Contains("warmup", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("warm up", StringComparison.OrdinalIgnoreCase))
        {
            return "WARMUP";
        }

        if (trimmed.Contains("race", StringComparison.OrdinalIgnoreCase))
        {
            return "RACE";
        }

        if (trimmed.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            return "TESTING";
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Splits a session into its label and its primary figure. Prefers the
    /// clock; falls back to a lap count; falls back again to no figure at all.
    /// The label is shortened by <see cref="ShortType"/> so the strip stays
    /// inside its width budget.
    /// </summary>
    public static SessionHeader Header(string sessionType, double timeRemainSeconds, int lapsRemain)
    {
        var label = ShortType(sessionType);

        if (TimeRemaining(timeRemainSeconds) is { } time)
        {
            return new SessionHeader(label, time);
        }

        return lapsRemain > 0
            ? new SessionHeader(label, lapsRemain.ToString(CultureInfo.InvariantCulture) + " LAPS")
            : new SessionHeader(label, string.Empty);
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
