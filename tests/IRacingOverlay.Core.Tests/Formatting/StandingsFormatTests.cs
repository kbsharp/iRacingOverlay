using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Tests.Formatting;

public class StandingsFormatTests
{
    [Theory]
    [InlineData(98.021, "1:38.021")]
    [InlineData(65.5, "1:05.500")]
    [InlineData(45.2, "0:45.200")]
    [InlineData(122.009, "2:02.009")]
    public void LapTime_FormatsMinutesSecondsMillis(double seconds, string expected)
    {
        Assert.Equal(expected, StandingsFormat.LapTime(seconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void LapTime_UnsetOrNonPositive_ReturnsPlaceholder(double? seconds)
    {
        Assert.Equal(TelemetryFormat.Placeholder, StandingsFormat.LapTime(seconds));
    }

    [Theory]
    [InlineData(2, "▲2")]
    [InlineData(11, "▲11")]
    [InlineData(-1, "▼1")]
    [InlineData(-7, "▼7")]
    public void PositionChange_ArrowCarriesTheSign(int gained, string expected)
    {
        Assert.Equal(expected, StandingsFormat.PositionChange(gained));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void PositionChange_UnchangedOrUnknown_IsBlank(int? gained)
    {
        Assert.Equal(string.Empty, StandingsFormat.PositionChange(gained));
    }

    [Fact]
    public void Gap_LapsDown_ShowsLapCount()
    {
        Assert.Equal("+2L", StandingsFormat.Gap(seconds: 5.0, lapsDown: 2));
    }

    [Fact]
    public void Gap_LapsDownTakesPrecedenceOverTime()
    {
        // A car a lap down should read "+1L", not a (misleading) sub-lap time.
        Assert.Equal("+1L", StandingsFormat.Gap(seconds: 0.4, lapsDown: 1));
    }

    [Fact]
    public void Gap_TimeGap_FormatsWithLeadingPlus()
    {
        Assert.Equal("+5.5", StandingsFormat.Gap(seconds: 5.53, lapsDown: 0));
    }

    [Fact]
    public void Gap_ClassLeader_IsBlank()
    {
        Assert.Equal(string.Empty, StandingsFormat.Gap(seconds: 0, lapsDown: 0));
    }

    [Fact]
    public void Gap_Unknown_ReturnsPlaceholder()
    {
        Assert.Equal(TelemetryFormat.Placeholder, StandingsFormat.Gap(seconds: null, lapsDown: 0));
    }
}
