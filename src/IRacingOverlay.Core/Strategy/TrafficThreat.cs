namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// A faster-class car closing on the player from behind, and where it reaches
/// them. Every figure is one the driver already owns and can check a lap later:
/// a car number, a class, a gap in seconds, and a sector off their own timing
/// screen. The forecast is falsifiable - a lap on, the car is where this said or
/// it isn't.
/// </summary>
/// <param name="CarNumber">The approaching car's number, e.g. "63".</param>
/// <param name="ClassShortName">Its class, e.g. "GTP" - the thing you yield to.</param>
/// <param name="ClassColorRaw">The class's iRacing colour, so the strip reads in
/// the same hue the relative and standings already use for that class.</param>
/// <param name="GapSeconds">How far behind on track the car is now, in seconds.</param>
/// <param name="ClosingRateSecondsPerLap">How much of that gap it takes back each
/// of the player's laps - the class-pace difference the forecast is built on.</param>
/// <param name="LapsToContact">Player laps until it arrives.</param>
/// <param name="MeetingSector">The 1-based sector the car reaches the player in,
/// or null when the sim hasn't reported sector boundaries.</param>
public readonly record struct TrafficThreat(
    string CarNumber,
    string ClassShortName,
    string? ClassColorRaw,
    double GapSeconds,
    double ClosingRateSecondsPerLap,
    double LapsToContact,
    int? MeetingSector)
{
    /// <summary>No incoming traffic to forecast: single-class, nobody faster
    /// behind, or the nearest one is still too many laps away to act on.</summary>
    public static TrafficThreat None { get; } =
        new(string.Empty, string.Empty, null, 0, 0, 0, null);

    /// <summary>True when there is a threat worth showing.</summary>
    public bool HasThreat => ClosingRateSecondsPerLap > 0;
}
