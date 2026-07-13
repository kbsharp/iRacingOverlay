namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// Rolling fuel-usage estimate. Values are null until at least one clean
/// lap has been recorded. <paramref name="LapsCounted"/> is the number of
/// laps currently contributing to the average.
/// </summary>
public readonly record struct FuelEstimate(
    double? AverageLitersPerLap,
    double? LastLapLiters,
    double? EstimatedLapsRemaining,
    int LapsCounted)
{
    public static FuelEstimate Empty { get; } = new(null, null, null, 0);
}
