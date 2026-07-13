using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Infrastructure.Telemetry;

/// <summary>
/// Fake telemetry for developing the overlay without iRacing running
/// (the --demo flag). Simulates short laps with a steady fuel burn so
/// the fuel widget populates quickly.
/// </summary>
public sealed class SimulatedTelemetrySource : ITelemetrySource
{
    private const double TickSeconds = 1.0 / 15; // match the live source's ~15 Hz
    private const double LapSeconds = 15;        // short laps so estimates appear fast
    private const float StartingFuelLiters = 45f;
    private const float BaseLitersPerLap = 2.4f;

    private readonly object _gate = new();

    private Timer? _timer;
    private double _sessionTime;
    private float _fuel = StartingFuelLiters;
    private bool _connectionAnnounced;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<bool>? ConnectionChanged;

    // The simulated source has no failure modes; the event exists only to
    // satisfy the contract.
#pragma warning disable CS0067
    public event EventHandler<Exception>? ErrorOccurred;
#pragma warning restore CS0067

    public void Start()
    {
        _timer ??= new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(TickSeconds));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private void Tick(object? state)
    {
        TelemetrySnapshot snapshot;
        var announceConnection = false;

        lock (_gate)
        {
            if (!_connectionAnnounced)
            {
                _connectionAnnounced = true;
                announceConnection = true;
            }

            _sessionTime += TickSeconds;

            var lap = (int)(_sessionTime / LapSeconds) + 1;
            var lapProgress = _sessionTime % LapSeconds / LapSeconds;

            // Vary the burn a little per lap so average and last-lap differ.
            var litersPerLap = BaseLitersPerLap * (1f + 0.06f * MathF.Sin(lap * 1.7f));
            _fuel = Math.Max(0f, _fuel - litersPerLap * (float)(TickSeconds / LapSeconds));

            var speed = 45.0 + 22.0 * Math.Sin(lapProgress * Math.PI * 4); // two straights per lap
            var gear = speed switch
            {
                < 30 => 3,
                < 45 => 4,
                < 58 => 5,
                _ => 6,
            };

            snapshot = new TelemetrySnapshot(
                SessionTimeSeconds: _sessionTime,
                Lap: lap,
                FuelLevelLiters: _fuel,
                SpeedMetersPerSecond: (float)speed,
                Gear: gear,
                IsOnTrack: true);
        }

        if (announceConnection)
        {
            ConnectionChanged?.Invoke(this, true);
        }

        TelemetryReceived?.Invoke(this, snapshot);
    }
}
