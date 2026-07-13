using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Tests.Formatting;

public class TelemetryFormatTests
{
    [Theory]
    [InlineData(-1, "R")]
    [InlineData(0, "N")]
    [InlineData(1, "1")]
    [InlineData(6, "6")]
    public void Gear_MapsSdkGearNumbersToDisplayText(int gear, string expected)
    {
        Assert.Equal(expected, TelemetryFormat.Gear(gear));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(27.78f, 100)]
    [InlineData(83.33f, 300)]
    public void ToKph_ConvertsMetersPerSecond(float metersPerSecond, int expectedKph)
    {
        Assert.Equal(expectedKph, TelemetryFormat.ToKph(metersPerSecond));
    }

    [Fact]
    public void Liters_Null_ReturnsPlaceholder()
    {
        Assert.Equal(TelemetryFormat.Placeholder, TelemetryFormat.Liters(null));
    }

    [Fact]
    public void Liters_Value_FormatsToTwoDecimalPlaces()
    {
        Assert.Equal("2.50", TelemetryFormat.Liters(2.5));
    }

    [Fact]
    public void Laps_Null_ReturnsPlaceholder()
    {
        Assert.Equal(TelemetryFormat.Placeholder, TelemetryFormat.Laps(null));
    }

    [Fact]
    public void Laps_Value_FormatsToOneDecimalPlace()
    {
        Assert.Equal("20.9", TelemetryFormat.Laps(20.94));
    }
}
