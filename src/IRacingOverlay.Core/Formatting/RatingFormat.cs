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

/// <summary>Rough iRating bands, used to give the rating badge visual weight.</summary>
public enum IRatingTier
{
    Low,
    Mid,
    High,
    Elite,
}

/// <summary>Pure classification helpers that drive the relative widget's colour coding.</summary>
public static class RatingFormat
{
    private const int MidThreshold = 1500;
    private const int HighThreshold = 2500;
    private const int EliteThreshold = 4000;

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

    public static IRatingTier ClassifyIRating(int irating) => irating switch
    {
        < MidThreshold => IRatingTier.Low,
        < HighThreshold => IRatingTier.Mid,
        < EliteThreshold => IRatingTier.High,
        _ => IRatingTier.Elite,
    };

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
}
