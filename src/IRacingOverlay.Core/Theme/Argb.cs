using System.Globalization;

namespace IRacingOverlay.Core.Theme;

/// <summary>
/// A straight 32-bit ARGB colour - the pure-domain currency the palette speaks in,
/// so <c>Core</c> can define and test the app's meaning-hues without a reference to
/// WPF's <c>System.Windows.Media.Color</c>. The App layer converts these to brushes.
/// Hex round-trips through <see cref="ToHex"/>/<see cref="Parse"/> so the values
/// read the same here as they do in App.xaml.
/// </summary>
public readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    /// <summary>Parses <c>#RRGGBB</c> (opaque) or <c>#AARRGGBB</c>. The leading
    /// <c>#</c> is optional. Culture-invariant, matching the repo's formatting rule.</summary>
    public static Argb Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        var span = hex.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        static byte Byte(ReadOnlySpan<char> s) =>
            byte.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return span.Length switch
        {
            6 => new Argb(0xFF, Byte(span[0..2]), Byte(span[2..4]), Byte(span[4..6])),
            8 => new Argb(Byte(span[0..2]), Byte(span[2..4]), Byte(span[4..6]), Byte(span[6..8])),
            _ => throw new FormatException($"'{hex}' is not a #RRGGBB or #AARRGGBB colour."),
        };
    }

    /// <summary>Always the 8-digit <c>#AARRGGBB</c> form, so
    /// <c>Parse(x.ToHex()) == x</c> for every colour including translucent ones.</summary>
    public string ToHex() =>
        string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");

    public override string ToString() => ToHex();
}
