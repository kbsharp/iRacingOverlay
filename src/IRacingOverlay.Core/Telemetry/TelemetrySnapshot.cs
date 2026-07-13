namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// A single frame of telemetry, normalised to the units the overlay works in.
/// </summary>
public sealed record TelemetrySnapshot(
    double SessionTimeSeconds,
    int Lap,
    float FuelLevelLiters,
    float SpeedMetersPerSecond,
    int Gear,
    bool IsOnTrack);
