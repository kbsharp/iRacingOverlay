using System.Globalization;

namespace IRacingOverlay.Core.Formatting;

/// <summary>Display formatting for the delta bar.</summary>
public static class DeltaFormat
{
    /// <summary>
    /// Formats a lap delta as "-0.34" / "+1.02", always signed and always to two
    /// decimals. The sign is the whole point of the readout, so it is never
    /// dropped; the value is rounded before the sign is taken so a delta of
    /// -0.002 renders "+0.00" rather than the nonsense "-0.00".
    /// </summary>
    public static string Signed(double seconds)
    {
        var rounded = Math.Round(seconds, 2, MidpointRounding.AwayFromZero);
        var sign = rounded < 0 ? "-" : "+";
        return sign + Math.Abs(rounded).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
