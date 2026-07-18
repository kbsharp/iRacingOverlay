using System.Globalization;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Unit-aware display formatting. Telemetry is normalised to metric on the way in
/// and every calculation stays metric; conversion happens here, at format time
/// only. That's what lets the unit preference be flipped mid-session without
/// invalidating a rolling fuel average or a lap-time window.
///
/// Numbers keep the same precision in both systems (fuel 2dp, temperature whole
/// degrees) so a column doesn't change width when the unit changes.
/// </summary>
public static class UnitFormat
{
    private const double GallonsPerLiter = 0.26417205235815;
    private const double MphPerMeterPerSecond = 2.2369362920544;
    private const double KphPerMeterPerSecond = 3.6;

    /// <summary>Fuel volume, converted from litres, or the placeholder for null.</summary>
    public static string Fuel(double? liters, FuelUnit unit)
    {
        if (liters is not { } value)
        {
            return TelemetryFormat.Placeholder;
        }

        var converted = unit == FuelUnit.Gallons ? value * GallonsPerLiter : value;
        return converted.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>The short unit label for a fuel figure ("L" / "gal").</summary>
    public static string FuelLabel(FuelUnit unit)
        => unit == FuelUnit.Gallons ? "gal" : "L";

    /// <summary>Temperature, converted from Celsius, as whole degrees with the
    /// degree sign - matching the relative widget's compact strip.</summary>
    public static string Temperature(float celsius, TemperatureUnit unit)
    {
        var converted = unit == TemperatureUnit.Fahrenheit
            ? celsius * 9f / 5f + 32f
            : celsius;

        return $"{MathF.Round(converted).ToString("0", CultureInfo.InvariantCulture)}°";
    }

    /// <summary>Speed, converted from metres per second, rounded to whole units.</summary>
    public static int Speed(float metersPerSecond, SpeedUnit unit)
    {
        var factor = unit == SpeedUnit.Mph ? MphPerMeterPerSecond : KphPerMeterPerSecond;
        return (int)Math.Round(metersPerSecond * factor);
    }

    /// <summary>The short unit label for a speed figure ("km/h" / "mph").</summary>
    public static string SpeedLabel(SpeedUnit unit)
        => unit == SpeedUnit.Mph ? "mph" : "km/h";
}
