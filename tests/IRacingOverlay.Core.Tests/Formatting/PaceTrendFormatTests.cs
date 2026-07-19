using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;

namespace IRacingOverlay.Core.Tests.Formatting;

public class PaceTrendFormatTests
{
    [Fact]
    public void Arrow_PointsDownWhenTheGapIsShrinking()
    {
        Assert.Equal("▼", PaceTrendFormat.Arrow(Closing(0.4, laps: 5, arrives: true)));
        Assert.Equal("▲", PaceTrendFormat.Arrow(Pulling(0.4)));
        Assert.Equal(string.Empty, PaceTrendFormat.Arrow(PaceTrend.None));
        Assert.Equal(string.Empty, PaceTrendFormat.Arrow(Holding()));
    }

    [Fact]
    public void Rate_IsUnsignedToOneDecimal()
    {
        Assert.Equal("0.4", PaceTrendFormat.Rate(Closing(0.42, laps: 5, arrives: true)));
        Assert.Equal("0.4", PaceTrendFormat.Rate(Pulling(0.44)));
    }

    [Fact]
    public void Rate_WithoutADirection_IsEmpty()
    {
        Assert.Equal(string.Empty, PaceTrendFormat.Rate(PaceTrend.None));
        Assert.Equal(string.Empty, PaceTrendFormat.Rate(Holding()));
    }

    [Fact]
    public void LapsToContact_RoundsDownSoTheForecastIsNotOptimistic()
    {
        Assert.Equal("5L", PaceTrendFormat.LapsToContact(Closing(0.4, laps: 5.9, arrives: true)));
    }

    [Fact]
    public void LapsToContact_LessThanOneLapAway_StillReadsAsOne()
    {
        Assert.Equal("1L", PaceTrendFormat.LapsToContact(Closing(0.4, laps: 0.3, arrives: true)));
    }

    [Fact]
    public void LapsToContact_BattleThatMissesTheFlag_ShowsNothing()
    {
        Assert.Equal(string.Empty, PaceTrendFormat.LapsToContact(Closing(0.4, laps: 12, arrives: false)));
    }

    [Fact]
    public void LapsToContact_UnknownArrival_StillShowsTheCountdown()
    {
        // Practice and open sessions have no flag to miss.
        Assert.Equal("6L", PaceTrendFormat.LapsToContact(Closing(0.4, laps: 6.4, arrives: null)));
    }

    [Fact]
    public void LapsToContact_NotClosing_ShowsNothing()
    {
        Assert.Equal(string.Empty, PaceTrendFormat.LapsToContact(Pulling(0.4)));
        Assert.Equal(string.Empty, PaceTrendFormat.LapsToContact(Holding()));
        Assert.Equal(string.Empty, PaceTrendFormat.LapsToContact(PaceTrend.None));
    }

    [Fact]
    public void Tone_CatchingSomeoneAhead_IsAGain()
    {
        Assert.Equal(
            PaceTrendTone.Gain,
            PaceTrendFormat.Tone(Closing(0.4, laps: 5, arrives: true), isAhead: true));
    }

    [Fact]
    public void Tone_BeingCaughtFromBehind_IsAThreat()
    {
        Assert.Equal(
            PaceTrendTone.Threat,
            PaceTrendFormat.Tone(Closing(0.4, laps: 5, arrives: true), isAhead: false));
    }

    [Fact]
    public void Tone_BattleThatMissesTheFlag_StaysQuiet()
    {
        Assert.Equal(
            PaceTrendTone.Neutral,
            PaceTrendFormat.Tone(Closing(0.4, laps: 12, arrives: false), isAhead: false));
    }

    [Fact]
    public void Tone_PullingOrHolding_StaysQuiet()
    {
        Assert.Equal(PaceTrendTone.Neutral, PaceTrendFormat.Tone(Pulling(0.4), isAhead: true));
        Assert.Equal(PaceTrendTone.Neutral, PaceTrendFormat.Tone(Holding(), isAhead: false));
        Assert.Equal(PaceTrendTone.Neutral, PaceTrendFormat.Tone(PaceTrend.None, isAhead: true));
    }

    private static PaceTrend Closing(double rate, double laps, bool? arrives) =>
        new(PaceTrendDirection.Closing, rate, laps, arrives);

    private static PaceTrend Pulling(double rate) =>
        new(PaceTrendDirection.Pulling, -rate, null, null);

    private static PaceTrend Holding() =>
        new(PaceTrendDirection.Holding, 0, null, null);
}
