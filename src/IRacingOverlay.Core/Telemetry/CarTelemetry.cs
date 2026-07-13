namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// Per-car state from iRacing's CarIdx* telemetry arrays.
/// <paramref name="EstTimeSeconds"/> is the sim's estimate of the time taken
/// to reach the car's current position on its lap (resets each lap).
/// </summary>
public readonly record struct CarTelemetry(
    int CarIdx,
    int Lap,
    float LapDistPct,
    float EstTimeSeconds,
    bool OnPitRoad,
    CarTrackSurface Surface,
    int Position);
