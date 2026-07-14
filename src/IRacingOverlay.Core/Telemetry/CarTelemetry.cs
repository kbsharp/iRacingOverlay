namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// Per-car state from iRacing's CarIdx* telemetry arrays.
/// <paramref name="EstTimeSeconds"/> is the sim's estimate of the time taken
/// to reach the car's current position on its lap (resets each lap).
/// The lap-time and <paramref name="F2TimeSeconds"/> fields feed the standings
/// widget; iRacing reports non-positive values (typically -1) when a car has
/// no valid time yet, which the calculators treat as "unknown".
/// </summary>
/// <param name="F2TimeSeconds">iRacing's CarIdxF2Time - in a race, the car's
/// time behind the session leader; other session types report a best lap time
/// here instead.</param>
public readonly record struct CarTelemetry(
    int CarIdx,
    int Lap,
    float LapDistPct,
    float EstTimeSeconds,
    bool OnPitRoad,
    CarTrackSurface Surface,
    int Position,
    int ClassPosition,
    int LapsCompleted,
    float BestLapTimeSeconds,
    float LastLapTimeSeconds,
    float F2TimeSeconds);
