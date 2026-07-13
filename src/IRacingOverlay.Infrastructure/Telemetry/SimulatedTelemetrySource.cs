using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Infrastructure.Telemetry;

/// <summary>
/// Fake telemetry for developing the overlay without iRacing running
/// (the --demo flag). Simulates a small field on short laps so the
/// relative and fuel widgets populate quickly.
/// </summary>
public sealed class SimulatedTelemetrySource : ITelemetrySource
{
    private const double TickSeconds = 1.0 / 15; // match the live source's ~15 Hz
    private const int PlayerIdx = 0;
    private const double PlayerLapSeconds = 15;  // short laps so estimates appear fast
    private const float StartingFuelLiters = 45f;
    private const float BaseLitersPerLap = 2.4f;

    /// <summary>Name, lap time, and starting progress (in laps) for the fake field.
    /// Offsets put one car a lap up, one a lap down, and one in the pits.</summary>
    private static readonly SimDriver[] Field =
    [
        new(0, "K. Bevan", "23", 2350, "B 3.44", 15.0, 5.30, InPits: false),
        new(1, "T. Ridley", "7", 2410, "B 3.02", 14.9, 5.42, InPits: false),
        new(2, "S. Okafor", "88", 2280, "C 2.77", 15.1, 5.18, InPits: false),
        new(3, "L. Fontaine", "4", 2590, "B 3.91", 14.8, 5.55, InPits: false),
        new(4, "R. Tanaka", "51", 2150, "C 2.31", 15.2, 5.05, InPits: false),
        new(5, "D. Whitmore", "12", 3105, "A 4.21", 14.6, 6.40, InPits: false), // lap ahead
        new(6, "K. Larsen", "31", 1720, "D 2.08", 15.6, 4.22, InPits: false),   // lap down
        new(7, "P. Moreau", "9", 2035, "C 2.50", 15.3, 5.68, InPits: false),
        new(8, "C. Ibarra", "77", 2760, "B 3.66", 14.7, 5.93, InPits: true),    // parked in pits
    ];

    private readonly object _gate = new();

    private Timer? _timer;
    private double _sessionTime;
    private float _fuel = StartingFuelLiters;
    private bool _connectionAnnounced;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<SessionMetadata>? SessionMetadataReceived;
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
            snapshot = BuildSnapshot();
        }

        if (announceConnection)
        {
            ConnectionChanged?.Invoke(this, true);
            SessionMetadataReceived?.Invoke(this, BuildMetadata());
        }

        TelemetryReceived?.Invoke(this, snapshot);
    }

    private TelemetrySnapshot BuildSnapshot()
    {
        var cars = new List<CarTelemetry>(Field.Length);
        var totalProgress = new double[Field.Length];

        foreach (var driver in Field)
        {
            totalProgress[driver.CarIdx] = driver.InPits
                ? driver.StartProgressLaps
                : driver.StartProgressLaps + _sessionTime / driver.LapSeconds;
        }

        foreach (var driver in Field)
        {
            var progress = totalProgress[driver.CarIdx];
            var lap = (int)progress + 1;
            var pct = (float)(progress - Math.Floor(progress));
            var position = 1 + totalProgress.Count(p => p > progress);

            cars.Add(new CarTelemetry(
                driver.CarIdx,
                lap,
                pct,
                EstTimeSeconds: (float)(pct * driver.LapSeconds),
                OnPitRoad: driver.InPits,
                driver.InPits ? CarTrackSurface.InPitStall : CarTrackSurface.OnTrack,
                position));
        }

        var player = cars[PlayerIdx];
        var playerPct = player.LapDistPct;

        // Vary the burn a little per lap so average and last-lap differ.
        var litersPerLap = BaseLitersPerLap * (1f + 0.06f * MathF.Sin(player.Lap * 1.7f));
        _fuel = Math.Max(0f, _fuel - litersPerLap * (float)(TickSeconds / PlayerLapSeconds));

        var speed = 45.0 + 22.0 * Math.Sin(playerPct * Math.PI * 4); // two straights per lap
        var gear = speed switch
        {
            < 30 => 3,
            < 45 => 4,
            < 58 => 5,
            _ => 6,
        };

        return new TelemetrySnapshot
        {
            SessionTimeSeconds = _sessionTime,
            SessionNum = 0,
            SessionTimeRemainSeconds = Math.Max(0, 20 * 60 - _sessionTime),
            SessionLapsRemain = -1,
            Lap = player.Lap,
            FuelLevelLiters = _fuel,
            SpeedMetersPerSecond = (float)speed,
            Gear = gear,
            IsOnTrack = true,
            PlayerCarIdx = PlayerIdx,
            AirTempC = 26.4f,
            TrackTempC = 41.2f,
            Wetness = TrackWetness.VeryLightlyWet,
            BrakeBiasPct = 54.2f,
            IncidentCount = 2,
            Cars = cars,
        };
    }

    private static SessionMetadata BuildMetadata()
    {
        var drivers = Field.ToDictionary(
            d => d.CarIdx,
            d => new RosterDriver(
                d.CarIdx,
                d.Name,
                d.Number,
                d.IRating,
                d.License,
                (float)d.LapSeconds));

        return new SessionMetadata(drivers, new Dictionary<int, string> { [0] = "Race" });
    }

    private sealed record SimDriver(
        int CarIdx,
        string Name,
        string Number,
        int IRating,
        string License,
        double LapSeconds,
        double StartProgressLaps,
        bool InPits);
}
