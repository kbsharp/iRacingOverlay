using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Formatting;

public class UnitFormatTests
{
    [Fact]
    public void Fuel_Liters_IsUnconverted()
        => Assert.Equal("42.50", UnitFormat.Fuel(42.5, FuelUnit.Liters));

    [Fact]
    public void Fuel_Gallons_ConvertsFromLiters()
    {
        // A 65 L tank is ~17.17 US gal.
        Assert.Equal("17.17", UnitFormat.Fuel(65, FuelUnit.Gallons));
    }

    [Theory]
    [InlineData(FuelUnit.Liters)]
    [InlineData(FuelUnit.Gallons)]
    public void Fuel_Null_IsThePlaceholderInEitherUnit(FuelUnit unit)
        => Assert.Equal(TelemetryFormat.Placeholder, UnitFormat.Fuel(null, unit));

    [Fact]
    public void Fuel_KeepsTwoDecimalsInBothUnits()
    {
        // Same precision either way, so the column doesn't change width when the
        // unit is flipped mid-session.
        Assert.Equal(5, UnitFormat.Fuel(65, FuelUnit.Liters).Length);
        Assert.Equal(5, UnitFormat.Fuel(65, FuelUnit.Gallons).Length);
    }

    [Fact]
    public void FuelLabel_MatchesTheUnit()
    {
        Assert.Equal("L", UnitFormat.FuelLabel(FuelUnit.Liters));
        Assert.Equal("gal", UnitFormat.FuelLabel(FuelUnit.Gallons));
    }

    [Fact]
    public void Temperature_Celsius_IsUnconverted()
        => Assert.Equal("28°", UnitFormat.Temperature(27.6f, TemperatureUnit.Celsius));

    [Theory]
    [InlineData(0f, "32°")]
    [InlineData(100f, "212°")]
    [InlineData(-40f, "-40°")]
    [InlineData(25f, "77°")]
    public void Temperature_Fahrenheit_ConvertsFromCelsius(float celsius, string expected)
        => Assert.Equal(expected, UnitFormat.Temperature(celsius, TemperatureUnit.Fahrenheit));

    [Fact]
    public void Speed_Kph_ConvertsFromMetersPerSecond()
        => Assert.Equal(180, UnitFormat.Speed(50f, SpeedUnit.Kph));

    [Fact]
    public void Speed_Mph_ConvertsFromMetersPerSecond()
        => Assert.Equal(112, UnitFormat.Speed(50f, SpeedUnit.Mph));

    [Fact]
    public void Speed_Kph_MatchesTheOlderTelemetryFormatHelper()
    {
        // UnitFormat supersedes TelemetryFormat.ToKph; they must not disagree.
        Assert.Equal(TelemetryFormat.ToKph(37.3f), UnitFormat.Speed(37.3f, SpeedUnit.Kph));
    }

    [Fact]
    public void SpeedLabel_MatchesTheUnit()
    {
        Assert.Equal("km/h", UnitFormat.SpeedLabel(SpeedUnit.Kph));
        Assert.Equal("mph", UnitFormat.SpeedLabel(SpeedUnit.Mph));
    }
}
