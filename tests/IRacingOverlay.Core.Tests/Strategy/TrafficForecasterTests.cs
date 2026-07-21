using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Strategy;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Strategy;

public class TrafficForecasterTests
{
    private const int PlayerIdx = 0;

    // A 100s reference lap, so an EstTime offset of N seconds is an N-second gap.
    private const double LapSeconds = 100.0;
    private const double PlayerPace = 100.0; // GT3
    private const double GtpPace = 96.0;     // 4s/lap faster
    private const double LmpPace = 92.0;     // 8s/lap faster

    private static readonly double[] ThreeSectors = [0.0, 0.4, 0.72];

    [Fact]
    public void FasterClassCarBehindAndClosing_IsForecast()
    {
        // Player mid-lap; a GTP 8s behind on track, closing 4s/lap -> 2 laps to
        // contact, meeting at 0.5 + 2.0 = 2.5 -> sector 2.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.True(result.HasThreat);
        Assert.Equal("1", result.CarNumber);
        Assert.Equal("GTP", result.ClassShortName);
        Assert.Equal(8, result.GapSeconds, precision: 3);
        Assert.Equal(4, result.ClosingRateSecondsPerLap, precision: 3);
        Assert.Equal(2, result.LapsToContact, precision: 3);
        Assert.Equal(2, result.MeetingSector);
    }

    [Fact]
    public void SameClassCarBehind_IsNotTraffic()
    {
        // Another GT3 behind is a battle for the relative, not traffic to yield to.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GT3", PlayerPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void SlowerClassBehind_IsNotTraffic()
    {
        // A slower class behind is never catching at pace - nothing to yield.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("Cup", 110.0)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void FasterCarAhead_IsNotForecast()
    {
        // Ahead on track: they are pulling away, not catching.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.55f, est: 58f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void TooManyLapsBehind_IsNotForecastYet()
    {
        // 40s back at 4s/lap is 10 laps away - a warning you can't act on.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.1f, est: 10f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void PicksTheMostImminentAmongSeveral()
    {
        // Two GTPs behind; the nearer in laps-to-contact wins.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.42f, est: 42f),  // 8s -> 2 laps
            Car(2, pct: 0.47f, est: 47f)); // 3s -> 0.75 laps

        var result = TrafficForecaster.Compute(
            snapshot, Roster(("GTP", GtpPace), ("GTP", GtpPace)));

        Assert.Equal("2", result.CarNumber);
        Assert.Equal(0.75, result.LapsToContact, precision: 3);
    }

    [Fact]
    public void MixedClasses_PicksTheGenuineThreat()
    {
        // An LMP2 further back but much faster can be more imminent than a nearer
        // GTP - the maths, not the raw gap, decides.
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.44f, est: 44f),  // GTP 6s / 4 = 1.5 laps
            Car(2, pct: 0.42f, est: 42f)); // LMP 8s / 8 = 1.0 lap

        var result = TrafficForecaster.Compute(
            snapshot, Roster(("GTP", GtpPace), ("LMP2", LmpPace)));

        Assert.Equal("2", result.CarNumber);
        Assert.Equal("LMP2", result.ClassShortName);
        Assert.Equal(1.0, result.LapsToContact, precision: 3);
    }

    [Fact]
    public void ApproachingCarInThePits_IsIgnored()
    {
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.45f, est: 42f, onPitRoad: true));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void PlayerInThePits_ProjectsNothing()
    {
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f, onPitRoad: true),
            Car(1, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void CarsAbsentFromRoster_AreIgnored()
    {
        // Car 9 is in the world and faster-looking but not in the roster (pace car).
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(9, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(snapshot, Roster(("GTP", GtpPace)));

        Assert.False(result.HasThreat);
    }

    [Fact]
    public void NoSectorBoundaries_StillForecastsWithoutASector()
    {
        var snapshot = Snapshot(
            Player(pct: 0.5f, est: 50f),
            Car(1, pct: 0.45f, est: 42f));

        var result = TrafficForecaster.Compute(
            snapshot, Roster(sectors: null, ("GTP", GtpPace)));

        Assert.True(result.HasThreat);
        Assert.Null(result.MeetingSector);
    }

    [Fact]
    public void NullMetadata_ProjectsNothing()
    {
        var snapshot = Snapshot(Player(pct: 0.5f, est: 50f), Car(1, pct: 0.45f, est: 42f));

        Assert.False(TrafficForecaster.Compute(snapshot, metadata: null).HasThreat);
    }

    private static CarTelemetry Player(float pct, float est, bool onPitRoad = false) =>
        Car(PlayerIdx, pct, est, onPitRoad);

    private static CarTelemetry Car(int carIdx, float pct, float est, bool onPitRoad = false) =>
        new(
            CarIdx: carIdx,
            Lap: 5,
            LapDistPct: pct,
            EstTimeSeconds: est,
            OnPitRoad: onPitRoad,
            Surface: onPitRoad ? CarTrackSurface.InPitStall : CarTrackSurface.OnTrack,
            Position: carIdx + 1,
            ClassPosition: carIdx + 1,
            LapsCompleted: 5,
            BestLapTimeSeconds: (float)LapSeconds,
            LastLapTimeSeconds: (float)LapSeconds,
            F2TimeSeconds: 0f);

    /// <summary>
    /// Builds a roster where the player (car 0) is GT3 and each later car takes a
    /// class from <paramref name="others"/> in order, with that class's pace.
    /// </summary>
    private static SessionMetadata Roster(params (string Class, double Pace)[] others) =>
        Roster(ThreeSectors, others);

    private static SessionMetadata Roster(
        double[]? sectors, params (string Class, double Pace)[] others)
    {
        var drivers = new Dictionary<int, RosterDriver>
        {
            [PlayerIdx] = Driver(PlayerIdx, "GT3", PlayerPace),
        };

        for (var i = 0; i < others.Length; i++)
        {
            var carIdx = i + 1;
            drivers[carIdx] = Driver(carIdx, others[i].Class, others[i].Pace);
        }

        return new SessionMetadata(
            DriversByCarIdx: drivers,
            SessionTypesByNum: new Dictionary<int, string> { [1] = "Race" },
            PlayerSetupName: "baseline",
            PlayerSetupIsModified: false,
            TrackLengthMeters: 5000,
            SectorStartPcts: sectors);
    }

    private static RosterDriver Driver(int carIdx, string className, double pace) =>
        new(
            CarIdx: carIdx,
            DisplayName: $"Driver {carIdx}",
            CarNumber: carIdx.ToString(),
            IRating: 2000,
            License: "A 3.5",
            ClassEstLapTimeSeconds: (float)pace,
            ClassShortName: className,
            ClassColorRaw: "ff9933");

    private static TelemetrySnapshot Snapshot(params CarTelemetry[] cars) =>
        new()
        {
            SessionTimeSeconds = 600,
            SessionNum = 1,
            SessionTimeRemainSeconds = 1800,
            SessionLapsRemain = 20,
            Lap = 5,
            FuelLevelLiters = 40,
            SpeedMetersPerSecond = 50,
            Gear = 4,
            IsOnTrack = true,
            PlayerCarIdx = PlayerIdx,
            AirTempC = 20,
            TrackTempC = 30,
            Wetness = TrackWetness.Dry,
            BrakeBiasPct = 52,
            IncidentCount = 0,
            CarLeftRight = CarLeftRight.Clear,
            Cars = cars,
        };
}
