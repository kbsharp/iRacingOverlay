using System.Globalization;

namespace IRacingOverlay.Core.Formatting;

/// <summary>Pure display-formatting helpers shared by any front end.</summary>
public static class TelemetryFormat
{
    public const string Placeholder = "–";

    public static string Gear(int gear) => gear switch
    {
        < 0 => "R",
        0 => "N",
        _ => gear.ToString(CultureInfo.InvariantCulture),
    };

    public static int ToKph(float metersPerSecond) =>
        (int)MathF.Round(metersPerSecond * 3.6f);

    public static string Liters(double? liters) =>
        liters?.ToString("0.00", CultureInfo.InvariantCulture) ?? Placeholder;

    public static string Laps(double? laps) =>
        laps?.ToString("0.0", CultureInfo.InvariantCulture) ?? Placeholder;
}
