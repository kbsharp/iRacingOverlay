using IRacingOverlay.Core.Demo;
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
    private const double TickSeconds = 1.0 / 30; // match the live source's ~30 Hz
    private const int PlayerIdx = 0;
    private const double PlayerLapSeconds = 15;  // short laps so estimates appear fast
    private const float StartingFuelLiters = 45f;
    private const float TankCapacityLiters = 65f;
    private const float CriticalFuelLiters = 2f;
    private const float BaseLitersPerLap = 2.4f;
    private const int MinCarCountValue = 3;

    // A licence-style incident cap and race distance, so the session strip shows
    // its "x/limit" and "Lx/y" forms in the demo rather than the bare fallbacks.
    private const int DemoIncidentLimit = 17;

    private const int DemoRaceLaps = 25;

    // The radar needs a real track length (to turn lap-fraction gaps into metres)
    // and a track shape (the player's heading around the lap) to place cars. Give
    // the demo a plausible short circuit that weaves through a few corners, so the
    // radar populates with a believably angled pack rather than a dead-straight
    // line. Track length is never shown as a number, only used for the geometry.
    private const double DemoTrackLengthMeters = 3000.0;

    // Lap-fraction spacing between adjacent cars in the pack around the player.
    // ~0.006 of a 3000 m lap = ~18 m per grid slot, so the nearest few land
    // inside the radar's range.
    private const double PackGapLaps = 0.006;

    // The demo laps are deliberately short (~15 s) so estimates appear fast, but
    // that would make the standings show silly "0:15" lap times. This gives the
    // F2Time gaps a realistic scale without touching the sim cadence; per-class
    // best-lap figures come from the active preset instead.
    private const double ReferenceLapSeconds = 100.0;

    private static readonly TrackWetness[] WetnessCycle =
    [
        TrackWetness.Dry,
        TrackWetness.VeryLightlyWet,
        TrackWetness.ModeratelyWet,
        TrackWetness.VeryWet,
    ];

    /// <summary>Flag states the dev panel cycles through, in the order a race
    /// tends to meet them.</summary>
    private static readonly SessionFlags[] FlagCycle =
    [
        // Green running with nothing raised - the resting state, so the strip
        // shows no flag pill until the dev panel cycles it.
        SessionFlags.Green,
        SessionFlags.Green | SessionFlags.GreenHeld,
        SessionFlags.Yellow | SessionFlags.Caution,
        SessionFlags.Blue,
        SessionFlags.White,
        SessionFlags.Checkered,
        SessionFlags.Repair,
        SessionFlags.Black,
    ];

    /// <summary>Driver names/numbers the field is drawn from, in order. The field
    /// is rebuilt from the active <see cref="RacePreset"/>; car 0 is always the
    /// player. Sized to a full 40-car grid, which caps <see cref="MaxCarCount"/>.</summary>
    private static readonly (string Name, string Number)[] RosterPool =
    [
        ("K. Bevan", "23"), ("T. Ridley", "7"), ("S. Okafor", "88"), ("L. Fontaine", "4"),
        ("R. Tanaka", "51"), ("D. Whitmore", "12"), ("K. Larsen", "31"), ("P. Moreau", "9"),
        ("C. Ibarra", "77"), ("A. Novak", "14"), ("E. Duarte", "22"), ("H. Kessler", "35"),
        ("N. Osei", "41"), ("V. Petrov", "56"), ("F. Laurent", "63"), ("Y. Takahashi", "70"),
        ("C. Bianchi", "82"), ("M. Alvarez", "91"), ("S. Lindqvist", "3"), ("D. Kowalski", "18"),
        ("G. Rossi", "5"), ("O. Bergström", "16"), ("T. Nguyen", "27"), ("J. Fischer", "33"),
        ("W. Park", "44"), ("L. Costa", "52"), ("B. Andersen", "61"), ("R. Haas", "68"),
        ("K. Yamamoto", "74"), ("Z. Popov", "80"), ("N. Dubois", "86"), ("A. Silva", "92"),
        ("M. Weber", "6"), ("E. Johansson", "19"), ("D. Marchetti", "28"), ("P. Sørensen", "36"),
        ("H. Meyer", "47"), ("F. Romano", "53"), ("C. Nielsen", "66"), ("V. Ivanov", "75"),
    ];

    /// <summary>Session types the dev panel's "Cycle Session" control steps through,
    /// each with the setup file you'd realistically have loaded for it - so cycling
    /// forward simulates exactly the transition the setup-reminder widget guards
    /// against (forgetting to swap off the practice/qualifying setup).</summary>
    private static readonly (string Type, string SetupFile)[] DemoSessions =
    [
        ("Practice", "practice_setup.sto"),
        ("Open Qualify", "qualify_setup.sto"),
        ("Race", "race_setup.sto"),
    ];

    private readonly object _gate = new();

    /// <summary>Serialises <see cref="Tick"/> against itself (see the comment
    /// there). Distinct from <see cref="_gate"/> so event handlers never run
    /// under the state lock the dev panel takes from the UI thread.</summary>
    private readonly object _tickGate = new();

    private readonly List<SimDriver> _field = [];

    private RacePreset _preset = RacePresets.Default;
    private int _presetIndex;

    private Timer? _timer;
    private double _sessionTime;
    private float _fuel = StartingFuelLiters;
    private int _wetnessIndex = 1; // VeryLightlyWet, matching the original fixed demo value
    private int _incidentCount = 2;
    private int _flagIndex;
    private bool _playerInPits;
    private bool _connectionAnnounced;

    // Starts already in "Race" to match this demo's original fixed behaviour
    // (a short timed race, so the fuel widget has a healthy margin by default).
    private int _sessionIndex = 2;
    private int _sessionNum = 2;
    private bool _setupModified;
    private CarLeftRight _carLeftRight = CarLeftRight.Clear;

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<SessionMetadata>? SessionMetadataReceived;
    public event EventHandler<bool>? ConnectionChanged;

    // The simulated source has no failure modes; the event exists only to
    // satisfy the contract.
#pragma warning disable CS0067
    public event EventHandler<Exception>? ErrorOccurred;
#pragma warning restore CS0067

    public SimulatedTelemetrySource()
    {
        RebuildField(_preset.DefaultCarCount);
    }

    public int CarCount
    {
        get { lock (_gate) { return _field.Count; } }
    }

    public int MinCarCount => MinCarCountValue;

    public int MaxCarCount => RosterPool.Length;

    public string CurrentRaceType
    {
        get { lock (_gate) { return _preset.Name; } }
    }

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

            RebuildField(_field.Count + 1);
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

            RebuildField(_field.Count - 1);
            return true;
        }
    }

    public string CycleRaceType()
    {
        lock (_gate)
        {
            _presetIndex = (_presetIndex + 1) % RacePresets.All.Count;
            _preset = RacePresets.All[_presetIndex];
            RebuildField(_preset.DefaultCarCount);
            return _preset.Name;
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

    public SessionFlagState CycleFlag()
    {
        lock (_gate)
        {
            _flagIndex = (_flagIndex + 1) % FlagCycle.Length;
            return SessionFlagResolver.Resolve(FlagCycle[_flagIndex]);
        }
    }

    public void TogglePlayerPit()
    {
        lock (_gate)
        {
            _playerInPits = !_playerInPits;
        }
    }

    public string CycleSessionType()
    {
        lock (_gate)
        {
            _sessionIndex = (_sessionIndex + 1) % DemoSessions.Length;
            _sessionNum++;
            _setupModified = false; // a freshly "loaded" setup starts unmodified
            return DemoSessions[_sessionIndex].Type;
        }
    }

    public void ToggleSetupModified()
    {
        lock (_gate)
        {
            _setupModified = !_setupModified;
        }
    }

    public CarLeftRight CycleCarLeftRight()
    {
        lock (_gate)
        {
            // Skip Off - that only ever happens in live mode before the sim
            // starts reporting; not a state worth cycling through in demo.
            _carLeftRight = _carLeftRight == CarLeftRight.TwoCarsRight
                ? CarLeftRight.Clear
                : (CarLeftRight)((int)_carLeftRight + 1);
            return _carLeftRight;
        }
    }

    private void Tick(object? state)
    {
        // System.Threading.Timer fires on the thread pool, so a handler that
        // outlasts the ~33ms period would overlap the next tick and consumers
        // would see concurrent events - the live source's single read loop never
        // does that, and consumers (RenderWidget's warm-up, any headless
        // harness) are entitled to the same sequential delivery. A tick that
        // arrives while the previous one is still delivering is dropped, not
        // queued: at 30Hz a skipped frame is indistinguishable from throttling.
        if (!Monitor.TryEnter(_tickGate))
        {
            return;
        }

        try
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
        finally
        {
            Monitor.Exit(_tickGate);
        }
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

        var leaderProgress = totalProgress.DefaultIfEmpty(0).Max();

        foreach (var driver in _field)
        {
            var inPits = IsInPits(driver);
            var progress = totalProgress[driver.CarIdx];
            var lap = (int)progress + 1;
            var pct = (float)(progress - Math.Floor(progress));
            var position = 1 + totalProgress.Count(p => p > progress);
            var classPosition = 1 + _field.Count(d =>
                d.ClassIndex == driver.ClassIndex && totalProgress[d.CarIdx] > progress);
            var bestLap = DemoBestLap(driver.ClassIndex, driver.CarIdx);
            var lastLap = bestLap + 0.3f + 0.9f * MathF.Abs(MathF.Sin((float)(_sessionTime * 0.2) + driver.CarIdx));

            cars.Add(new CarTelemetry(
                driver.CarIdx,
                lap,
                pct,
                EstTimeSeconds: (float)(pct * driver.LapSeconds),
                OnPitRoad: inPits,
                inPits ? CarTrackSurface.InPitStall : CarTrackSurface.OnTrack,
                position,
                ClassPosition: classPosition,
                LapsCompleted: (int)Math.Floor(progress),
                BestLapTimeSeconds: bestLap,
                LastLapTimeSeconds: lastLap,
                // F2Time is "time behind the session leader"; derive it from the
                // track-position gap scaled by a realistic reference lap time.
                F2TimeSeconds: (float)Math.Max(0, (leaderProgress - progress) * ReferenceLapSeconds)));
        }

        var player = cars[PlayerIdx];
        var playerPct = player.LapDistPct;

        // Vary the burn a little per lap so average and last-lap figures differ.
        var litersPerLap = BaseLitersPerLap * (1f + 0.06f * MathF.Sin(player.Lap * 1.7f));
        _fuel = Math.Clamp(_fuel - litersPerLap * (float)(TickSeconds / PlayerLapSeconds), 0f, TankCapacityLiters);

        // A plausible lap delta: it accumulates across the lap towards a per-lap
        // outcome, with a little wobble through the corners, and lands on that
        // outcome at the line (the wobble term is zero at pct 0 and 1) - which is
        // what the calculator banks and holds. No reference lap exists until the
        // player has completed one, so lap 1 reports "not valid" like the sim does.
        var lapOutcome = 0.9 * Math.Sin(player.Lap * 1.9);
        var lapDelta = lapOutcome * playerPct + 0.12 * Math.Sin(playerPct * Math.PI * 6);

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
            SessionNum = _sessionNum,
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
            Flags = FlagCycle[_flagIndex],
            CarLeftRight = _carLeftRight,
            PlayerYawRad = DemoHeading(playerPct),
            LapDeltaToBestSeconds = lapDelta,
            LapDeltaToBestValid = player.Lap > 1 && !_playerInPits,
            Cars = cars,
        };
    }

    /// <summary>The demo track's heading (radians) at a point on the lap - a weaving
    /// circuit with a few corners. The radar records this as the player laps and
    /// reuses it to place the field, so cars visibly angle through the corners and
    /// run parallel on the straights.</summary>
    private static float DemoHeading(double lapDistPct) =>
        (float)(1.5 * Math.Sin(2 * Math.PI * lapDistPct * 3));

    private bool IsInPits(SimDriver driver) => driver.CarIdx == PlayerIdx ? _playerInPits : driver.InPits;

    /// <summary>Rebuilds the field for the active preset at the given size, clamped to
    /// [<see cref="MinCarCount"/>, <see cref="MaxCarCount"/>]. Car 0 is the player, in
    /// the preset's player class; the rest are split across classes by the planner.
    /// Deterministic, so the same (preset, count) always yields the same grid.
    /// Callers must hold <see cref="_gate"/>.</summary>
    private void RebuildField(int carCount)
    {
        carCount = Math.Clamp(carCount, MinCarCountValue, MaxCarCount);
        var classByCar = DemoFieldPlanner.PlanClassByCar(_preset, carCount);
        var playerClassLap = _preset.Classes[_preset.PlayerClassIndex].BaseLapSeconds;

        _field.Clear();
        for (var carIdx = 0; carIdx < carCount; carIdx++)
        {
            var classIndex = classByCar[carIdx];
            var (name, number) = RosterPool[carIdx];
            var iRating = 1400 + carIdx * 173 % 2600;
            var licenseLetter = "ABCD"[carIdx % 4];
            var licenseValue = 2.0 + carIdx * 0.37 % 3.0;
            var license = $"{licenseLetter} {licenseValue:0.00}";

            // Keep the demo's short ~15 s cadence laps, but scale by the class's
            // relative pace so a faster class visibly laps a slower one.
            var paceFactor = _preset.Classes[classIndex].BaseLapSeconds / playerClassLap;
            var jitter = 1.0 + 0.02 * ((carIdx % 5) - 2);
            var lapSeconds = PlayerLapSeconds * paceFactor * jitter;

            // Seat the field in a tight pack straddling the player (car 0),
            // alternating ahead/behind, so the relative and radar widgets show a
            // believable battle. Cars 1 and 2 are then shoved a full lap ahead /
            // behind to keep the relative widget's lap-up / lap-down states (a car
            // a lap away correctly falls off the radar), and the last car parks in
            // the pits below - the three showcase states every widget relies on.
            var rank = (carIdx + 1) / 2;
            var placeAhead = carIdx % 2 == 1;
            var startProgress = 5.30 + (placeAhead ? 1 : -1) * rank * PackGapLaps;
            if (carIdx == 1)
            {
                startProgress += 1.0;
            }
            else if (carIdx == 2)
            {
                startProgress = Math.Max(0.2, startProgress - 1.0);
            }

            var inPits = carIdx == carCount - 1 && carCount > MinCarCountValue;

            _field.Add(new SimDriver(
                carIdx, name, number, iRating, license, classIndex, lapSeconds, startProgress, inPits));
        }
    }

    /// <summary>A deterministic, realistic-looking best lap per car for the standings,
    /// anchored to the car's class base lap in the active preset.</summary>
    private float DemoBestLap(int classIndex, int carIdx)
    {
        var baseLap = _preset.Classes[classIndex].BaseLapSeconds;
        return (float)(baseLap + (carIdx % 5) * 0.18 + (carIdx * 0.041 % 0.7));
    }

    private SessionMetadata BuildMetadata()
    {
        Dictionary<int, RosterDriver> drivers;

        lock (_gate)
        {
            drivers = _field.ToDictionary(
                d => d.CarIdx,
                d =>
                {
                    var carClass = _preset.Classes[d.ClassIndex];
                    var carPath = carClass.CarPaths.Count > 0
                        ? carClass.CarPaths[d.CarIdx % carClass.CarPaths.Count]
                        : string.Empty;
                    return new RosterDriver(
                        d.CarIdx, d.Name, d.Number, d.IRating, d.License, (float)d.LapSeconds,
                        carClass.ShortName, carClass.ColorHex, carPath);
                });
        }

        int sessionNum;
        string sessionType;
        string setupFile;
        bool setupModified;

        lock (_gate)
        {
            sessionNum = _sessionNum;
            sessionType = DemoSessions[_sessionIndex].Type;
            setupFile = DemoSessions[_sessionIndex].SetupFile;
            setupModified = _setupModified;
        }

        return new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [sessionNum] = sessionType },
            setupFile,
            setupModified,
            DemoTrackLengthMeters,
            DemoIncidentLimit,
            new Dictionary<int, int> { [sessionNum] = DemoRaceLaps },
            TankCapacityLiters);
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
