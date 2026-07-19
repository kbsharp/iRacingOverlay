using System.Globalization;

namespace IRacingOverlay.Core.Formatting;

/// <summary>Display formatting for the standings table.</summary>
public static class StandingsFormat
{
    /// <summary>Formats a lap time as "m:ss.fff", or the placeholder when unset
    /// (null or non-positive - iRacing reports -1 before a valid lap).</summary>
    public static string LapTime(double? seconds)
    {
        if (seconds is not > 0)
        {
            return TelemetryFormat.Placeholder;
        }

        var time = TimeSpan.FromSeconds(seconds.Value);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}");
    }

    /// <summary>
    /// Formats a car's gap to its class leader: "+n.n" for a time gap, "+nL"
    /// when a whole lap or more down, an empty string for the class leader
    /// itself, and the placeholder when the gap isn't known yet.
    /// </summary>
    public static string Gap(double? seconds, int lapsDown)
    {
        if (lapsDown > 0)
        {
            return "+" + lapsDown.ToString(CultureInfo.InvariantCulture) + "L";
        }

        if (seconds is null)
        {
            return TelemetryFormat.Placeholder;
        }

        if (seconds.Value <= 0)
        {
            return string.Empty; // class leader
        }

        return "+" + seconds.Value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats places gained since the start as "▲2" / "▼1". A car sitting on its
    /// starting position - and a car with no known start, outside a race - renders
    /// as nothing at all: an unchanged column of dashes down a 20-car field is
    /// noise, and the arrows are only worth their space where something moved.
    /// The glyph carries the sign, so the number stays unsigned.
    /// </summary>
    public static string PositionChange(int? gained) => gained switch
    {
        > 0 => "▲" + gained.Value.ToString(CultureInfo.InvariantCulture),
        < 0 => "▼" + (-gained.Value).ToString(CultureInfo.InvariantCulture),
        _ => string.Empty,
    };
}
