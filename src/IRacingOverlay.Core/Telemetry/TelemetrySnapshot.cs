namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// A single frame of telemetry, normalised to the units the overlay works in.
/// </summary>
public sealed record TelemetrySnapshot
{
    public required double SessionTimeSeconds { get; init; }

    public required int SessionNum { get; init; }

    /// <summary>Seconds left in the session; iRacing reports ~604800 when there is no time limit.</summary>
    public required double SessionTimeRemainSeconds { get; init; }

    /// <summary>Laps left in the session; negative when unknown/unlimited.</summary>
    public required int SessionLapsRemain { get; init; }

    public required int Lap { get; init; }

    public required float FuelLevelLiters { get; init; }

    public required float SpeedMetersPerSecond { get; init; }

    public required int Gear { get; init; }

    public required bool IsOnTrack { get; init; }

    public required int PlayerCarIdx { get; init; }

    public required float AirTempC { get; init; }

    public required float TrackTempC { get; init; }

    public required TrackWetness Wetness { get; init; }

    /// <summary>In-car brake bias, percent front; 0 when the car has no adjustable bias.</summary>
    public required float BrakeBiasPct { get; init; }

    public required int IncidentCount { get; init; }

    /// <summary>Raised track/personal flags; reduce with <see cref="SessionFlagResolver"/>.</summary>
    public SessionFlags Flags { get; init; }

    /// <summary>Near-field proximity from iRacing's own spotter signal - see <see cref="Telemetry.CarLeftRight"/>.</summary>
    public required CarLeftRight CarLeftRight { get; init; }

    /// <summary>The player car's heading in radians (iRacing's <c>Yaw</c>), world frame.
    /// Only the player's heading is available - the radar records it around the lap to
    /// reconstruct the track shape (see <c>Core.Radar.TrackMap</c>). Defaults to 0.</summary>
    public float PlayerYawRad { get; init; }

    /// <summary>Cars currently in the world (the player included).</summary>
    public required IReadOnlyList<CarTelemetry> Cars { get; init; }
}
