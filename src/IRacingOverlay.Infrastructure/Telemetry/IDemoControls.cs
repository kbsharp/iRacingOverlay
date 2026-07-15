using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Infrastructure.Telemetry;

/// <summary>
/// Live controls for exercising the overlay against simulated data, without
/// iRacing or a rebuild. Implemented only by <see cref="SimulatedTelemetrySource"/>;
/// the app checks for this interface and only shows dev controls when present.
/// </summary>
public interface IDemoControls
{
    int CarCount { get; }

    int MinCarCount { get; }

    int MaxCarCount { get; }

    /// <summary>Display name of the current race type preset.</summary>
    string CurrentRaceType { get; }

    /// <summary>Advances to the next race type preset (Mazda MX-5 Cup, GT3, IMSA
    /// multiclass, ...), rebuilding the field with that series' classes and typical
    /// grid size. Returns the new preset's display name.</summary>
    string CycleRaceType();

    /// <summary>Adds one car to the field. Returns false if already at <see cref="MaxCarCount"/>.</summary>
    bool AddCar();

    /// <summary>Removes the most recently added car. Returns false if already at <see cref="MinCarCount"/>.</summary>
    bool RemoveCar();

    /// <summary>Adds (or subtracts, for a negative value) fuel, clamped to the tank capacity.</summary>
    void AdjustFuel(float deltaLiters);

    /// <summary>Drops fuel to a near-empty level, for testing the "laps short" fuel state.</summary>
    void SetFuelCritical();

    /// <summary>Advances to the next track wetness level, wrapping around. Returns the new value.</summary>
    TrackWetness CycleWetness();

    void AddIncident();

    /// <summary>Flips whether the player's own car is shown as pitting.</summary>
    void TogglePlayerPit();

    /// <summary>Advances Practice -> Qualify -> Race -> Practice, each with its own
    /// matching setup file, as a new "session" (bumping the session number so the
    /// setup-reminder flash restarts). Returns the new session type name.</summary>
    string CycleSessionType();

    /// <summary>Flips whether the loaded setup is shown as modified.</summary>
    void ToggleSetupModified();

    /// <summary>Advances Clear -> CarLeft -> CarRight -> CarLeftRight -> TwoCarsLeft
    /// -> TwoCarsRight -> Clear, for exercising the radar widget. Returns the new value.</summary>
    CarLeftRight CycleCarLeftRight();
}
