namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// The strategic fuel picture for the rest of the race - the numbers a driver
/// acts on. All values are null until there is both a fuel-per-lap average and
/// a known race length.
/// </summary>
/// <param name="RaceLapsRemaining">Whole laps left in the race.</param>
/// <param name="FuelToFinishLiters">Fuel required to reach the finish.</param>
/// <param name="MarginLaps">Laps of fuel in hand at the finish; negative means short.</param>
/// <param name="FuelToAddLiters">Fuel to add at the next stop to finish with the safety buffer; 0 if already enough. Capped at a tankful (capacity) when capacity is known, so it never names more litres than a stop can take.</param>
/// <param name="AdditionalStops">Pit stops still needed after the next one. 0 when a single stop (or none) covers the race; positive only once the fuel to finish outruns one tankful, which is what turns the capped "add" figure honest.</param>
/// <param name="SaveTargetLitersPerLap">Max burn per lap that still reaches the finish on current fuel.</param>
/// <param name="WillFinish">Whether current fuel lasts to the finish at the current burn.</param>
public readonly record struct FuelStrategy(
    double? RaceLapsRemaining,
    double? FuelToFinishLiters,
    double? MarginLaps,
    double? FuelToAddLiters,
    int AdditionalStops,
    double? SaveTargetLitersPerLap,
    bool WillFinish)
{
    public static FuelStrategy Unknown { get; } = new(null, null, null, null, 0, null, false);
}
