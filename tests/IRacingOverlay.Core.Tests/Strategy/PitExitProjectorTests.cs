using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Strategy;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Strategy;

public class PitExitProjectorTests
{
    private const int PlayerIdx = 0;
    private const double PitLoss = 30.0;

    [Fact]
    public void NoLearnedPitLoss_ProjectsNothing()
    {
        // Better silent than guessed: the whole answer is scaled by this number.
        var snapshot = Snapshot(Car(PlayerIdx, f2: 20), Car(1, f2: 10));

        var result = PitExitProjector.Compute(snapshot, Roster(), pitLossSeconds: null);

        Assert.False(result.HasProjection);
    }

    [Fact]
    public void NotARace_ProjectsNothing()
    {
        // F2Time carries a best lap time outside a race, not a gap to the leader.
        var snapshot = Snapshot(Car(PlayerIdx, f2: 20), Car(1, f2: 10));

        var result = PitExitProjector.Compute(snapshot, Roster("Open Qualify"), PitLoss);

        Assert.False(result.HasProjection);
    }

    [Fact]
    public void RejoinsBehindTheCarsThePitLossHandsThePositionTo()
    {
        // Player 20s behind the leader; stopping costs 30s, so they come out 50s
        // back - behind the cars at 10s, 35s and 45s, ahead of the one at 70s.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 10),
            Car(2, f2: 35),
            Car(3, f2: 45),
            Car(4, f2: 70));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.True(result.HasProjection);
        Assert.Equal(4, result.ClassPosition);
        Assert.Equal(4, result.OverallPosition);
        Assert.Equal(30, result.PitLossSeconds);
    }

    [Fact]
    public void ReportsPositionsLostAgainstWhereTheyAreNow()
    {
        // Currently 2nd (only the car at 10s ahead); rejoining 4th costs two places.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 10),
            Car(2, f2: 35),
            Car(3, f2: 45),
            Car(4, f2: 70));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(2, result.PositionsLost);
    }

    [Fact]
    public void EmptyRoadBehind_CostsNoPositions()
    {
        // Nobody within the pit loss, so the stop is free in position terms - the
        // answer that actually tells a driver to go now.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 10),
            Car(2, f2: 200));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(2, result.ClassPosition);
        Assert.Equal(0, result.PositionsLost);
    }

    [Fact]
    public void NamesTheCarAheadAndBehindWithTheirGaps()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 45),
            Car(2, f2: 58));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        // Exits at 50s: 5s behind #1, with #2 8s further back.
        Assert.Equal("1", result.CarAheadNumber);
        Assert.Equal(5, result.GapToCarAheadSeconds!.Value, precision: 3);
        Assert.Equal("2", result.CarBehindNumber);
        Assert.Equal(8, result.GapToCarBehindSeconds!.Value, precision: 3);
    }

    [Fact]
    public void RejoiningAtTheFrontOfTheClass_HasNoCarAhead()
    {
        var snapshot = Snapshot(Car(PlayerIdx, f2: 5), Car(1, f2: 90));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(1, result.ClassPosition);
        Assert.Equal(string.Empty, result.CarAheadNumber);
        Assert.Null(result.GapToCarAheadSeconds);
        Assert.Equal("1", result.CarBehindNumber);
    }

    [Fact]
    public void RejoiningLast_HasNoCarBehind()
    {
        var snapshot = Snapshot(Car(PlayerIdx, f2: 20), Car(1, f2: 10));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(2, result.ClassPosition);
        Assert.Equal(string.Empty, result.CarBehindNumber);
        Assert.Null(result.GapToCarBehindSeconds);
    }

    [Fact]
    public void ClassPositionCountsOnlyYourOwnClass()
    {
        // A GT3 driver's race is against GT3s; the prototypes streaming past are
        // traffic, not positions.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 30),
            Car(2, f2: 40),
            Car(3, f2: 45));

        var metadata = Roster(classesByCarIdx: new Dictionary<int, string>
        {
            [PlayerIdx] = "GT3",
            [1] = "LMP2",
            [2] = "LMP2",
            [3] = "GT3",
        });

        var result = PitExitProjector.Compute(snapshot, metadata, PitLoss);

        // Exits at 50s, behind all three overall but behind only one GT3.
        Assert.Equal(4, result.OverallPosition);
        Assert.Equal(2, result.ClassPosition);
        Assert.Equal("3", result.CarAheadNumber);
    }

    [Fact]
    public void LappedCarsSortByTimeWithoutCountingLaps()
    {
        // A car a lap down already carries a whole lap time in its F2Time, so it
        // lands behind the player without laps being counted separately.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 115, lap: 0, lapsCompleted: 0));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(1, result.ClassPosition);
        Assert.Equal("1", result.CarBehindNumber);
        Assert.Equal(65, result.GapToCarBehindSeconds!.Value, precision: 3);
    }

    [Fact]
    public void CarsWithNoClassifiedGap_AreIgnored()
    {
        // iRacing reports a negative F2Time before a car has a gap; counting one
        // as "ahead" would invent a position.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: -1),
            Car(2, f2: 10));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(2, result.ClassPosition);
    }

    [Fact]
    public void PaceCarsAndSpectators_AreIgnored()
    {
        // Car 9 is in the world with a gap but absent from the roster.
        var snapshot = Snapshot(
            Car(PlayerIdx, f2: 20),
            Car(1, f2: 10),
            Car(9, f2: 15));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.Equal(2, result.ClassPosition);
    }

    [Fact]
    public void PlayerWithNoClassifiedGap_ProjectsNothing()
    {
        var snapshot = Snapshot(Car(PlayerIdx, f2: -1), Car(1, f2: 10));

        var result = PitExitProjector.Compute(snapshot, Roster(), PitLoss);

        Assert.False(result.HasProjection);
    }

    private static CarTelemetry Car(
        int carIdx, double f2, int lap = 1, int lapsCompleted = 1) =>
        new(
            CarIdx: carIdx,
            Lap: lap,
            LapDistPct: 0.5f,
            EstTimeSeconds: 45,
            OnPitRoad: false,
            Surface: CarTrackSurface.OnTrack,
            Position: carIdx + 1,
            ClassPosition: carIdx + 1,
            LapsCompleted: lapsCompleted,
            BestLapTimeSeconds: 90,
            LastLapTimeSeconds: 91,
            F2TimeSeconds: (float)f2);

    private static SessionMetadata Roster(
        string sessionType = "Race",
        IReadOnlyDictionary<int, string>? classesByCarIdx = null)
    {
        var drivers = new Dictionary<int, RosterDriver>();
        foreach (var carIdx in new[] { PlayerIdx, 1, 2, 3, 4 })
        {
            var className = classesByCarIdx is not null && classesByCarIdx.TryGetValue(carIdx, out var c)
                ? c
                : "GT3";

            drivers[carIdx] = new RosterDriver(
                CarIdx: carIdx,
                DisplayName: $"Driver {carIdx}",
                CarNumber: carIdx.ToString(),
                IRating: 2000,
                License: "A 3.5",
                ClassEstLapTimeSeconds: 90,
                ClassShortName: className,
                ClassColorRaw: "ff9933");
        }

        return new SessionMetadata(
            DriversByCarIdx: drivers,
            SessionTypesByNum: new Dictionary<int, string> { [1] = sessionType },
            PlayerSetupName: "baseline",
            PlayerSetupIsModified: false,
            TrackLengthMeters: 5000);
    }

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
