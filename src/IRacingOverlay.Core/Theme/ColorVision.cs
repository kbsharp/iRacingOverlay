namespace IRacingOverlay.Core.Theme;

/// <summary>A red-green colour vision deficiency to simulate.</summary>
public enum ColorVisionDeficiency
{
    /// <summary>Full colour vision - the identity.</summary>
    None,

    /// <summary>Missing/anomalous M (green) cones - the most common CVD.</summary>
    Deuteranopia,

    /// <summary>Missing/anomalous L (red) cones - dims long wavelengths (red) most.</summary>
    Protanopia,
}

/// <summary>
/// Turns "is this readable to a colour-blind driver?" from a judgement call into a
/// checkable number - the repo's habit of instrumenting a thing rather than
/// reasoning about it (see the standings' time-based laps-down, the fuel-save fit).
/// It simulates how a dichromat sees a colour and measures how far apart two colours
/// land, so the palette tests can assert that the meaning-hues that share a column
/// stay distinct under deuteranopia and protanopia.
///
/// The dichromat matrices are the widely-used approximation applied directly to
/// sRGB (Vischeck/"colorblindly" lineage). They are an approximation, not a
/// clinical model - accurate enough to catch a pair that collapses, which is all the
/// invariant needs. The distance is plain Euclidean sRGB: it folds hue and
/// brightness into one "how perceptibly different" proxy, which is exactly the
/// question (the default gain/loss pair fails on both at once).
/// </summary>
public static class ColorVision
{
    // Rows are the simulated R, G, B as linear combinations of the input R, G, B.
    private static readonly double[,] DeuteranopiaMatrix =
    {
        { 0.625, 0.375, 0.0 },
        { 0.700, 0.300, 0.0 },
        { 0.000, 0.300, 0.7 },
    };

    private static readonly double[,] ProtanopiaMatrix =
    {
        { 0.567, 0.433, 0.0 },
        { 0.558, 0.442, 0.0 },
        { 0.000, 0.242, 0.758 },
    };

    /// <summary>How the given deficiency would render <paramref name="color"/>.
    /// Alpha is preserved. <see cref="ColorVisionDeficiency.None"/> returns it
    /// unchanged.</summary>
    public static Argb Simulate(Argb color, ColorVisionDeficiency deficiency)
    {
        if (deficiency == ColorVisionDeficiency.None)
        {
            return color;
        }

        var m = deficiency == ColorVisionDeficiency.Deuteranopia
            ? DeuteranopiaMatrix
            : ProtanopiaMatrix;

        double r = color.R, g = color.G, b = color.B;

        return color with
        {
            R = Clamp(m[0, 0] * r + m[0, 1] * g + m[0, 2] * b),
            G = Clamp(m[1, 0] * r + m[1, 1] * g + m[1, 2] * b),
            B = Clamp(m[2, 0] * r + m[2, 1] * g + m[2, 2] * b),
        };
    }

    /// <summary>Straight Euclidean distance in sRGB (0-255 per channel). ~441 max
    /// (black↔white). A rough "how perceptibly different" proxy - good enough to
    /// tell a distinct pair from a collapsed one.</summary>
    public static double Distance(Argb a, Argb b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>Distance between two colours <b>as a dichromat would see them</b> -
    /// simulate both under <paramref name="deficiency"/>, then measure. This is the
    /// number the palette invariant is built on.</summary>
    public static double DistanceUnder(Argb a, Argb b, ColorVisionDeficiency deficiency) =>
        Distance(Simulate(a, deficiency), Simulate(b, deficiency));

    /// <summary>WCAG relative luminance (0 black … 1 white), on linearized sRGB.
    /// The audit measured the default gain/loss collapse partly in these terms.</summary>
    public static double RelativeLuminance(Argb color) =>
        0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);

    private static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
