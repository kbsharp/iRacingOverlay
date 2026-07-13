using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Infrastructure.Telemetry;

/// <summary>
/// Fake telemetry for developing the overlay without iRacing running
/// (the --demo flag). Simulates a field on short laps so the relative and
/// fuel widgets populate quickly, and implements <see cref="IDemoControls"/>
/// so a dev panel can add cars, drain fuel, etc. live.
/// </summary>
public sealed class SimulatedTelemetrySource : ITelemetrySource, IDemoControls
{
    private const double TickSeconds = 1.0 / 15; // match the live source's ~15 Hz
    private const int PlayerIdx = 0;
    private const double PlayerLapSeconds = 15;  // short laps so estimates appear fast
    private const float StartingFuelLiters = 45f;
    private const float TankCapacityLiters = 65f;
    private const float CriticalFuelLiters = 2f;
    private const float BaseLitersPerLap = 2.4f;
    private const int MinCarCountValue = 3;

    private static readonly TrackWetness[] WetnessCycle =
    [
        TrackWetness.Dry,
        TrackWetness.VeryLightlyWet,
        TrackWetness.ModeratelyWet,
        TrackWetness.VeryWet,
    ];

    /// <summary>Fake multiclass field so the relative widget's class colouring is
    /// visible in demo mode too, without needing a live multiclass session.</summary>
    private static readonly (string Name, string ColorHex)[] DemoClasses =
    [
        ("GT3", "E8579E"),
        ("GTE", "3AC6D9"),
        ("DP", "FFC94D"),
    ];

    /// <summary>Initial field: name, number, iRating, license, class index (into
    /// <see cref="DemoClasses"/>), lap time, starting progress (in laps), and pit
    /// state. Offsets put one car a lap up, one a lap down, and one parked in the
    /// pits, so every relative-widget state is visible immediately.</summary>
    private static readonly SimDriver[] InitialField =
    [
        new(0, "K. Bevan", "23", 2350, "B 3.44", 0, 15.0, 5.30, InPits: false),
        new(1, "T. Ridley", "7", 2410, "B 3.02", 0, 14.9, 5.42, InPits: false),
        new(2, "S. Okafor", "88", 2280, "C 2.77", 0, 15.1, 5.18, InPits: false),
        new(3, "L. Fontaine", "4", 2590, "B 3.91", 0, 14.8, 5.55, InPits: false),
        new(4, "R. Tanaka", "51", 2150, "C 2.31", 0, 15.2, 5.05, InPits: false),
        new(5, "D. Whitmore", "12", 3105, "A 4.21", 1, 14.6, 6.40, InPits: false), // lap ahead
        new(6, "K. Larsen", "31", 1720, "D 2.08", 1, 15.6, 4.22, InPits: false),   // lap down
        new(7, "P. Moreau", "9", 2035, "C 2.50", 2, 15.3, 5.68, InPits: false),
        new(8, "C. Ibarra", "77", 2760, "B 3.66", 2, 14.7, 5.93, InPits: true),    // parked in pits
    ];

    /// <summary>Extra drivers the dev panel can add on top of <see cref="InitialField"/>.
    /// Caps <see cref="MaxCarCount"/> at InitialField.Length + this array's length.</summary>
    private static readonly (string Name, string Number)[] ExtraRoster =
    [
        ("A. Novak", "14"), ("E. Duarte", "22"), ("H. Kessler", "35"), ("N. Osei", "41"),
        ("V. Petrov", "56"), ("F. Laurent", "63"), ("Y. Takahashi", "70"), ("C. Bianchi", "82"),
        ("M. Alvarez", "91"), ("S. Lindqvist", "3"), ("D. Kowalski", "18"),
    ];

    private readonly object _gate = new();
    private readonly List<SimDriver> _field = [.. InitialField];

    private Timer? _timer;
    private double _sessionTime;
    private float _fuel = StartingFuelLiters;
    private int _wetnessIndex = 1; // VeryLightlyWet, matching the original fixed demo value
    private int _incidentCount = 2;
    private bool _playerInPits;
    private bool _connectionAnnounced;
    private int _extrasAdded;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<SessionMetadata>? SessionMetadataReceived;
    public event EventHandler<bool>? ConnectionChanged;

    // The simulated source has no failure modes; the event exists only to
    // satisfy the contract.
#pragma warning disable CS0067
    public event EventHandler<Exception>? ErrorOccurred;
#pragma warning restore CS0067

    public int CarCount
    {
        get { lock (_gate) { return _field.Count; } }
    }

    public int MinCarCount => MinCarCountValue;

    public int MaxCarCount => InitialField.Length + ExtraRoster.Length;

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

    public bool AddCar()
    {
        lock (_gate)
        {
            if (_field.Count >= MaxCarCount)
            {
                return false;
            }

            var carIdx = _field.Count;
            // Indexed by a counter that only ever increases, not by the current field
            // size - RemoveCar can shrink the field below InitialField.Length, which
            // would otherwise make this index go negative after a remove-then-add.
            var (name, number) = ExtraRoster[_extrasAdded % ExtraRoster.Length];
            var classIndex = _extrasAdded % DemoClasses.Length;
            _extrasAdded++;
            var lapSeconds = PlayerLapSeconds * (1.0 + 0.02 * ((carIdx % 5) - 2));
            var startProgress = 4.0 + (carIdx % 10) * 0.55;
            var iRating = 1400 + carIdx * 173 % 2600;
            var licenseLetter = "ABCD"[carIdx % 4];
            var licenseValue = 2.0 + carIdx * 0.37 % 3.0;
            var license = $"{licenseLetter} {licenseValue:0.00}";

            _field.Add(new SimDriver(
                carIdx, name, number, iRating, license, classIndex, lapSeconds, startProgress, InPits: false));
            return true;
        }
    }

    public bool RemoveCar()
    {
        lock (_gate)
        {
            if (_field.Count <= MinCarCount)
            {
                return false;
            }

            _field.RemoveAt(_field.Count - 1);
            return true;
        }
    }

    public void AdjustFuel(float deltaLiters)
    {
        lock (_gate)
        {
            _fuel = Math.Clamp(_fuel + deltaLiters, 0f, TankCapacityLiters);
        }
    }

    public void SetFuelCritical()
    {
        lock (_gate)
        {
            _fuel = CriticalFuelLiters;
        }
    }

    public TrackWetness CycleWetness()
    {
        lock (_gate)
        {
            _wetnessIndex = (_wetnessIndex + 1) % WetnessCycle.Length;
            return WetnessCycle[_wetnessIndex];
        }
    }

    public void AddIncident()
    {
        lock (_gate)
        {
            _incidentCount++;
        }
    }

    public void TogglePlayerPit()
    {
        lock (_gate)
        {
            _playerInPits = !_playerInPits;
        }
    }

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
        }

        // The roster changes whenever the dev panel adds/removes a car, so
        // re-broadcast metadata every tick rather than once at connect time.
        SessionMetadataReceived?.Invoke(this, BuildMetadata());
        TelemetryReceived?.Invoke(this, snapshot);
    }

    /// <summary>Called under <see cref="_gate"/> from <see cref="Tick"/>.</summary>
    private TelemetrySnapshot BuildSnapshot()
    {
        var cars = new List<CarTelemetry>(_field.Count);
        var totalProgress = new double[_field.Count];

        foreach (var driver in _field)
        {
            var inPits = IsInPits(driver);
            totalProgress[driver.CarIdx] = inPits
                ? driver.StartProgressLaps
                : driver.StartProgressLaps + _sessionTime / driver.LapSeconds;
        }

        foreach (var driver in _field)
        {
            var inPits = IsInPits(driver);
            var progress = totalProgress[driver.CarIdx];
            var lap = (int)progress + 1;
            var pct = (float)(progress - Math.Floor(progress));
            var position = 1 + totalProgress.Count(p => p > progress);

            cars.Add(new CarTelemetry(
                driver.CarIdx,
                lap,
                pct,
                EstTimeSeconds: (float)(pct * driver.LapSeconds),
                OnPitRoad: inPits,
                inPits ? CarTrackSurface.InPitStall : CarTrackSurface.OnTrack,
                position));
        }

        var player = cars[PlayerIdx];
        var playerPct = player.LapDistPct;

        // Vary the burn a little per lap so average and last-lap figures differ.
        var litersPerLap = BaseLitersPerLap * (1f + 0.06f * MathF.Sin(player.Lap * 1.7f));
        _fuel = Math.Clamp(_fuel - litersPerLap * (float)(TickSeconds / PlayerLapSeconds), 0f, TankCapacityLiters);

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
            // A ~4 minute timed race so fuel-to-finish shows a healthy margin by default.
            SessionTimeRemainSeconds = Math.Max(0, 4 * 60 - _sessionTime),
            SessionLapsRemain = -1,
            Lap = player.Lap,
            FuelLevelLiters = _fuel,
            SpeedMetersPerSecond = (float)speed,
            Gear = gear,
            IsOnTrack = true,
            PlayerCarIdx = PlayerIdx,
            AirTempC = 26.4f,
            TrackTempC = 41.2f,
            Wetness = WetnessCycle[_wetnessIndex],
            BrakeBiasPct = 54.2f,
            IncidentCount = _incidentCount,
            Cars = cars,
        };
    }

    private bool IsInPits(SimDriver driver) => driver.CarIdx == PlayerIdx ? _playerInPits : driver.InPits;

    private SessionMetadata BuildMetadata()
    {
        Dictionary<int, RosterDriver> drivers;

        lock (_gate)
        {
            drivers = _field.ToDictionary(
                d => d.CarIdx,
                d =>
                {
                    var (className, classColorHex) = DemoClasses[d.ClassIndex];
                    return new RosterDriver(
                        d.CarIdx, d.Name, d.Number, d.IRating, d.License, (float)d.LapSeconds,
                        className, classColorHex);
                });
        }

        return new SessionMetadata(drivers, new Dictionary<int, string> { [0] = "Race" });
    }

    private sealed record SimDriver(
        int CarIdx,
        string Name,
        string Number,
        int IRating,
        string License,
        int ClassIndex,
        double LapSeconds,
        double StartProgressLaps,
        bool InPits);
}
