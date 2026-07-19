using System.Globalization;

namespace IRacingOverlay.Core.Formatting;

/// <summary>iRacing's official license classes, lowest to highest.</summary>
public enum LicenseTier
{
    Unknown,
    Rookie,
    D,
    C,
    B,
    A,
    Pro,
}

/// <summary>Which way a projected iRating change is going.</summary>
public enum RatingTrend
{
    Flat,
    Up,
    Down,
}

/// <summary>Pure classification helpers that drive the relative widget's colour coding.</summary>
public static class RatingFormat
{
    /// <summary>Reads the license class from an iRacing LicString ("A 4.99", "Rookie", "Pro").</summary>
    public static LicenseTier ParseLicenseTier(string? license)
    {
        if (string.IsNullOrWhiteSpace(license))
        {
            return LicenseTier.Unknown;
        }

        return char.ToUpperInvariant(license.Trim()[0]) switch
        {
            'R' => LicenseTier.Rookie,
            'D' => LicenseTier.D,
            'C' => LicenseTier.C,
            'B' => LicenseTier.B,
            'A' => LicenseTier.A,
            'P' => LicenseTier.Pro,
            _ => LicenseTier.Unknown,
        };
    }

    public static RatingTrend ClassifyTrend(int delta) => delta switch
    {
        > 0 => RatingTrend.Up,
        < 0 => RatingTrend.Down,
        _ => RatingTrend.Flat,
    };

    /// <summary>
    /// The magnitude of a projected iRating change, unsigned - the arrow beside
    /// it carries the direction, so repeating it as a "+" reads as noise.
    /// </summary>
    public static string DeltaMagnitude(int delta) =>
        Math.Abs(delta).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Corners per incident point, as a whole number - the fractional part of a
    /// figure in the dozens-to-hundreds is noise, and it has to sit in a chip.
    /// Caps the display at "999+" so a nearly-clean session can't stretch the
    /// strip; the caller shows a clean session as words rather than a number.
    /// </summary>
    public static string Cpi(double cpi)
    {
        if (double.IsNaN(cpi) || cpi <= 0)
        {
            return "0";
        }

        return cpi >= 999.5
            ? "999+"
            : Math.Round(cpi).ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Normalises iRacing's CarClassColor - reported as a decimal-packed 0xRRGGBB
    /// integer (e.g. "16777215") - to a "#RRGGBB" string. Also accepts an
    /// already-hex value defensively. Returns null for anything unrecognised so
    /// the caller can fall back to a neutral colour.
    /// </summary>
    public static string? NormalizeHexColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim().TrimStart('#');

        if (trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit)
            && int.TryParse(trimmed, out var packed) && packed >= 0)
        {
            return "#" + (packed & 0xFFFFFF).ToString("X6");
        }

        if (trimmed.Length is 6 or 8 && trimmed.All(Uri.IsHexDigit))
        {
            return "#" + trimmed[..6].ToUpperInvariant();
        }

        return null;
    }

    /// <summary>
    /// Whether text drawn on top of a solid block of the given colour should be
    /// dark rather than light. Used by the standings class name-plate: iRacing's
    /// class colours are series-defined and can be anything, so a plate filled
    /// with a dark navy would swallow dark text entirely.
    ///
    /// Uses relative luminance with the standard Rec. 601 weights - the eye is
    /// far more sensitive to green than to blue, so a plain RGB average would
    /// call a saturated blue "light" and a saturated green "dark". The 0.55
    /// threshold sits above the midpoint because light text on a mid-tone holds
    /// up better than dark text does on the same tone.
    /// </summary>
    public static bool PrefersDarkText(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        var trimmed = hex.Trim().TrimStart('#');
        if (trimmed.Length < 6 || !trimmed[..6].All(Uri.IsHexDigit))
        {
            return false;
        }

        var r = int.Parse(trimmed[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = int.Parse(trimmed[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = int.Parse(trimmed[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        var luminance = ((0.299 * r) + (0.587 * g) + (0.114 * b)) / 255.0;
        return luminance >= 0.55;
    }
}
