using IRacingOverlay.Core.Theme;

namespace IRacingOverlay.Core.Tests.Theme;

public class ColorVisionTests
{
    private static readonly Argb White = new(0xFF, 255, 255, 255);
    private static readonly Argb Black = new(0xFF, 0, 0, 0);

    [Fact]
    public void NoneIsTheIdentity()
    {
        var color = Argb.Parse("#33D689");
        Assert.Equal(color, ColorVision.Simulate(color, ColorVisionDeficiency.None));
    }

    [Theory]
    [InlineData(ColorVisionDeficiency.Deuteranopia)]
    [InlineData(ColorVisionDeficiency.Protanopia)]
    public void GreysArePreserved(ColorVisionDeficiency deficiency)
    {
        // Each simulation matrix' rows sum to 1, so a neutral maps to itself - a
        // dichromat sees greys unchanged, which is the sanity check on the matrices.
        Assert.Equal(White, ColorVision.Simulate(White, deficiency));
        Assert.Equal(Black, ColorVision.Simulate(Black, deficiency));
    }

    [Fact]
    public void SimulationPreservesAlpha()
    {
        var translucent = Argb.Parse("#80FF5C6C");
        Assert.Equal(0x80, ColorVision.Simulate(translucent, ColorVisionDeficiency.Protanopia).A);
    }

    [Fact]
    public void RelativeLuminanceSpansBlackToWhite()
    {
        Assert.Equal(0.0, ColorVision.RelativeLuminance(Black), 3);
        Assert.Equal(1.0, ColorVision.RelativeLuminance(White), 3);
    }

    [Fact]
    public void DistanceIsZeroForEqualColoursAndMaximalForBlackWhite()
    {
        Assert.Equal(0.0, ColorVision.Distance(White, White));
        Assert.Equal(Math.Sqrt(3 * 255.0 * 255.0), ColorVision.Distance(Black, White), 3);
    }

    [Fact]
    public void DefaultGainAndLossCollapseUnderRedGreenDeficiency()
    {
        // The motivating measurement: iRacing-style green vs red, which read as
        // opposite signals with full colour vision, land much closer together once
        // green/red sensitivity is gone. This is the gap the colour-blind palette
        // exists to close - asserted so a "harmless" tweak to the default pair that
        // made the collapse worse would be caught.
        var green = MeaningPalette.Color(PaletteVariant.Default, MeaningColor.Positive);
        var red = MeaningPalette.Color(PaletteVariant.Default, MeaningColor.Negative);

        var full = ColorVision.Distance(green, red);
        var deutan = ColorVision.DistanceUnder(green, red, ColorVisionDeficiency.Deuteranopia);
        var protan = ColorVision.DistanceUnder(green, red, ColorVisionDeficiency.Protanopia);

        Assert.True(deutan < full, $"deutan {deutan:0} should be < full-vision {full:0}");
        Assert.True(protan < full, $"protan {protan:0} should be < full-vision {full:0}");
    }
}
