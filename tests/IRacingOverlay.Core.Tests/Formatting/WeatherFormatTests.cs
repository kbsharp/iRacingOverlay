using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Core.Weather;

namespace IRacingOverlay.Core.Tests.Formatting;

public class WeatherFormatTests
{
    [Theory]
    [InlineData(TrackWetness.Dry, "Dry")]
    [InlineData(TrackWetness.MostlyDry, "Mostly Dry")]
    [InlineData(TrackWetness.VeryLightlyWet, "Very Lightly Wet")]
    [InlineData(TrackWetness.LightlyWet, "Lightly Wet")]
    [InlineData(TrackWetness.ModeratelyWet, "Moderately Wet")]
    [InlineData(TrackWetness.VeryWet, "Very Wet")]
    [InlineData(TrackWetness.ExtremelyWet, "Extremely Wet")]
    public void WetnessLabel_NamesEachLevelInFull(TrackWetness wetness, string expected)
        => Assert.Equal(expected, WeatherFormat.WetnessLabel(wetness));

    [Fact]
    public void WetnessLabel_Unknown_IsPlaceholder()
        => Assert.Equal(TelemetryFormat.Placeholder, WeatherFormat.WetnessLabel(TrackWetness.Unknown));

    [Theory]
    [InlineData(WeatherTrend.Wetting, "WETTING")]
    [InlineData(WeatherTrend.Drying, "DRYING")]
    [InlineData(WeatherTrend.Steady, "")]
    [InlineData(WeatherTrend.Insufficient, "")]
    public void TrendLabel_HeadlinesOnlyTheActiveTransitions(WeatherTrend trend, string expected)
        => Assert.Equal(expected, WeatherFormat.TrendLabel(trend));

    [Theory]
    [InlineData(300, "5 min")]
    [InlineData(90, "2 min")]   // 1.5 min rounds away from zero
    [InlineData(61, "1 min")]
    [InlineData(10, "1 min")]   // floored at one, never "0 min"
    public void MinutesAgo_RoundsToWholeMinutesWithAFloorOfOne(double seconds, string expected)
        => Assert.Equal(expected, WeatherFormat.MinutesAgo(seconds));
}
