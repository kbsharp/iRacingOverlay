using IRacingOverlay.Core.Theme;

namespace IRacingOverlay.Core.Tests.Theme;

public class MeaningPaletteTests
{
    // A separation both dichromats must clear for two signals that share a column.
    // The colour-blind pairs land comfortably above this (~170-210); the threshold
    // sits well below that so a real regression trips it, not simulation rounding.
    private const double DistinctThreshold = 120.0;

    [Fact]
    public void BothVariantsDefineEverySignal()
    {
        foreach (var variant in Enum.GetValues<PaletteVariant>())
        {
            var colors = MeaningPalette.For(variant);
            foreach (var signal in Enum.GetValues<MeaningColor>())
            {
                Assert.True(colors.ContainsKey(signal), $"{variant} is missing {signal}");
            }
        }
    }

    [Fact]
    public void DefaultVariantMatchesTheAppXamlHexes()
    {
        // Guards against the palette and App.xaml drifting apart - these are the
        // literals App.xaml paints today.
        var d = MeaningPalette.For(PaletteVariant.Default);
        Assert.Equal("#FF33D689", d[MeaningColor.Positive].ToHex());
        Assert.Equal("#FFFF5C6C", d[MeaningColor.Negative].ToHex());
        Assert.Equal("#FFC08BFF", d[MeaningColor.FastestLap].ToHex());
        Assert.Equal("#FF37D14A", d[MeaningColor.RadarPlayer].ToHex());
    }

    [Theory]
    [InlineData(ColorVisionDeficiency.Deuteranopia)]
    [InlineData(ColorVisionDeficiency.Protanopia)]
    public void ColorBlindGainAndLossStayDistinct(ColorVisionDeficiency deficiency)
    {
        var gain = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.Positive);
        var loss = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.Negative);

        Assert.True(
            ColorVision.DistanceUnder(gain, loss, deficiency) >= DistinctThreshold,
            $"gain/loss collapse under {deficiency}");
    }

    [Theory]
    [InlineData(ColorVisionDeficiency.Deuteranopia)]
    [InlineData(ColorVisionDeficiency.Protanopia)]
    public void ColorBlindLapTintsStayDistinct(ColorVisionDeficiency deficiency)
    {
        var ahead = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.LapAhead);
        var behind = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.LapBehind);

        Assert.True(
            ColorVision.DistanceUnder(ahead, behind, deficiency) >= DistinctThreshold,
            $"lap-ahead/lap-behind collapse under {deficiency}");
    }

    [Theory]
    [InlineData(ColorVisionDeficiency.Deuteranopia)]
    [InlineData(ColorVisionDeficiency.Protanopia)]
    public void ColorBlindGainLossSeparatesBetterThanTheDefault(ColorVisionDeficiency deficiency)
    {
        // The whole point of the preset: under red-green deficiency the new pair must
        // read further apart than the one it replaces.
        var cbGain = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.Positive);
        var cbLoss = MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, MeaningColor.Negative);
        var defGain = MeaningPalette.Color(PaletteVariant.Default, MeaningColor.Positive);
        var defLoss = MeaningPalette.Color(PaletteVariant.Default, MeaningColor.Negative);

        Assert.True(
            ColorVision.DistanceUnder(cbGain, cbLoss, deficiency)
            > ColorVision.DistanceUnder(defGain, defLoss, deficiency),
            $"colour-blind pair no better than default under {deficiency}");
    }

    [Theory]
    [InlineData(MeaningColor.LapBehind)]
    [InlineData(MeaningColor.RadarPlayer)]
    public void SignalsThatAlreadySurviveAreLeftUnchanged(MeaningColor signal)
    {
        // Blue lap-behind and the green player mark already hold up under CVD (and
        // the player mark is spatially unambiguous), so the preset deliberately
        // leaves them be - documented here so a future palette edit is intentional.
        Assert.Equal(
            MeaningPalette.Color(PaletteVariant.Default, signal),
            MeaningPalette.Color(PaletteVariant.ColorBlindFriendly, signal));
    }

    [Theory]
    [InlineData(PaletteVariant.Default)]
    [InlineData(PaletteVariant.ColorBlindFriendly)]
    public void DangerGlowHasThreeStopsWithATransparentEdge(PaletteVariant variant)
    {
        var stops = MeaningPalette.DangerGlow(variant);
        Assert.Equal(3, stops.Count);
        Assert.Equal(0.0, stops[0].Offset);
        Assert.Equal(1.0, stops[^1].Offset);
        Assert.Equal(0xFF, stops[0].Color.A);  // hot core
        Assert.Equal(0x00, stops[^1].Color.A); // fades to nothing
    }
}
