namespace IRacingOverlay.Core.Settings;

/// <summary>Fuel volume unit. iRacing reports litres; gallons are US gallons,
/// which is what the sim itself shows for US-locale users.</summary>
public enum FuelUnit
{
    Liters = 0,
    Gallons = 1,
}

/// <summary>Temperature unit for the relative widget's track/air readout.</summary>
public enum TemperatureUnit
{
    Celsius = 0,
    Fahrenheit = 1,
}

/// <summary>Speed unit. Not currently rendered by any widget - carried here so
/// the preference exists ahead of the delta bar / speed readout.</summary>
public enum SpeedUnit
{
    Kph = 0,
    Mph = 1,
}

/// <summary>
/// The user's display units. Telemetry is always normalised to metric on the way
/// in (see <c>TelemetrySnapshot</c>) and converted only at format time, so a unit
/// change never touches a calculation - it can be flipped mid-session and every
/// widget just re-renders.
/// </summary>
public sealed record UnitPreferences
{
    public FuelUnit Fuel { get; init; } = FuelUnit.Liters;

    public TemperatureUnit Temperature { get; init; } = TemperatureUnit.Celsius;

    public SpeedUnit Speed { get; init; } = SpeedUnit.Kph;

    /// <summary>Clamps unrecognised enum values (a hand-edited or future-version
    /// settings file deserializes out-of-range ints happily) back to metric.</summary>
    public UnitPreferences Sanitized() => new()
    {
        Fuel = Enum.IsDefined(Fuel) ? Fuel : FuelUnit.Liters,
        Temperature = Enum.IsDefined(Temperature) ? Temperature : TemperatureUnit.Celsius,
        Speed = Enum.IsDefined(Speed) ? Speed : SpeedUnit.Kph,
    };
}
