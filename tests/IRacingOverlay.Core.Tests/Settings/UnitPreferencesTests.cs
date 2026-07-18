using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class UnitPreferencesTests
{
    [Fact]
    public void Defaults_AreMetric()
    {
        var units = new UnitPreferences();

        Assert.Equal(FuelUnit.Liters, units.Fuel);
        Assert.Equal(TemperatureUnit.Celsius, units.Temperature);
        Assert.Equal(SpeedUnit.Kph, units.Speed);
    }

    [Fact]
    public void Sanitized_KeepsValidChoices()
    {
        var units = new UnitPreferences
        {
            Fuel = FuelUnit.Gallons,
            Temperature = TemperatureUnit.Fahrenheit,
            Speed = SpeedUnit.Mph,
        };

        Assert.Equal(units, units.Sanitized());
    }

    [Fact]
    public void Sanitized_UndefinedEnumValue_FallsBackToMetric()
    {
        // System.Text.Json deserializes an out-of-range int straight into an enum
        // field without complaint, so a hand-edited or future-version file can
        // land here with a value no switch arm handles.
        var units = new UnitPreferences
        {
            Fuel = (FuelUnit)42,
            Temperature = (TemperatureUnit)(-1),
            Speed = (SpeedUnit)99,
        }.Sanitized();

        Assert.Equal(FuelUnit.Liters, units.Fuel);
        Assert.Equal(TemperatureUnit.Celsius, units.Temperature);
        Assert.Equal(SpeedUnit.Kph, units.Speed);
    }
}
