using System.Globalization;

namespace IRacingOverlay.Core.Radar;

/// <summary>
/// Parses iRacing's session-info track length (e.g. <c>"3.70 km"</c>, occasionally
/// in miles) into metres. The radar needs a real distance to turn each car's
/// lap-fraction gap into how many car lengths away it actually is.
/// </summary>
public static class TrackLengthParser
{
    private const double MetresPerKilometre = 1000.0;
    private const double MetresPerMile = 1609.344;

    /// <summary>Metres, or 0 when the value is missing or unparseable.</summary>
    public static double ParseToMeters(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0.0;
        }

        var text = raw.Trim();
        var factor = MetresPerKilometre;

        if (text.EndsWith("mi", StringComparison.OrdinalIgnoreCase))
        {
            factor = MetresPerMile;
            text = text[..^2];
        }
        else if (text.EndsWith("km", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }
        else if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            factor = 1.0;
            text = text[..^1];
        }

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && value > 0
            ? value * factor
            : 0.0;
    }
}
