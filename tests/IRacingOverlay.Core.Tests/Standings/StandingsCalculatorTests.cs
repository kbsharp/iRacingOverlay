using IRacingOverlay.Core.Cars;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Standings;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Standings;

public class StandingsCalculatorTests
{
    // A two-class field. GT3 (idx 0,1,2) holds the overall leader; GTE (idx 3,4).
    // Overall positions interleave the classes on track.
    private static TelemetrySnapshot MulticlassSnapshot(int playerCarIdx = 1) => Snapshot(
        playerCarIdx,
        Car(0, position: 1, classPosition: 1, lapsCompleted: 10, best: 90.0f, last: 90.5f, f2: 0f),
        Car(1, position: 3, classPosition: 2, lapsCompleted: 10, best: 90.2f, last: 91.0f, f2: 5.0f),
        // A full lap-and-a-bit behind the GT3 leader on time (95s gap vs a 90s lap).
        Car(2, position: 5, classPosition: 3, lapsCompleted: 9, best: 91.0f, last: 92.0f, f2: 95.0f),
        Car(3, position: 2, classPosition: 1, lapsCompleted: 10, best: 95.0f, last: 95.5f, f2: 2.0f),
        Car(4, position: 4, classPosition: 2, lapsCompleted: 10, best: 95.5f, last: 96.0f, f2: 8.0f));

    private static SessionMetadata MulticlassRoster() => Roster(
        (0, "GT3"), (1, "GT3"), (2, "GT3"), (3, "GTE"), (4, "GTE"));

    [Fact]
    public void Compute_GroupsByClass_ClassOfOverallLeaderFirst()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        Assert.Equal(2, groups.Count);
        Assert.Equal("GT3", groups[0].ClassShortName);
        Assert.Equal("GTE", groups[1].ClassShortName);
    }

    [Fact]
    public void Compute_WithinClass_OrderedByPosition()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        Assert.Equal(new[] { 0, 1, 2 }, groups[0].Rows.Select(r => r.CarIdx));
        Assert.Equal(new[] { 3, 4 }, groups[1].Rows.Select(r => r.CarIdx));
    }

    [Fact]
    public void Compute_ClassLeader_FlaggedWithZeroGap()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        var gt3Leader = groups[0].Rows[0];
        Assert.True(gt3Leader.IsClassLeader);
        Assert.Equal(0, gt3Leader.GapToClassLeaderSeconds);

        // The GTE leader is behind the overall leader on F2Time, but its gap to
        // its own class leader (itself) is still zero.
        var gteLeader = groups[1].Rows[0];
        Assert.True(gteLeader.IsClassLeader);
        Assert.Equal(0, gteLeader.GapToClassLeaderSeconds);
    }

    [Fact]
    public void Compute_NonLeaderGap_IsRelativeToClassLeader()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        Assert.Equal(5.0, groups[0].Rows[1].GapToClassLeaderSeconds!.Value, 3);
        // GTE second is F2 8.0 behind a class leader at F2 2.0 -> 6.0 within class.
        Assert.Equal(6.0, groups[1].Rows[1].GapToClassLeaderSeconds!.Value, 3);
    }

    [Fact]
    public void Compute_LapsDown_FromTimeGapVersusClassLeaderBestLap()
    {
        // car2 is 95 s behind the GT3 leader whose best lap is 90 s -> one lap down.
        // car1 is only 5 s back -> same lap.
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        Assert.Equal(1, Assert.Single(groups[0].Rows, r => r.CarIdx == 2).GapLapsDown);
        Assert.Equal(0, Assert.Single(groups[0].Rows, r => r.CarIdx == 1).GapLapsDown);
    }

    [Fact]
    public void Compute_LapsDown_FallsBackToLapCountWhenNoLapTimeKnown()
    {
        // No best lap for anyone -> the calculator can't turn the gap into laps,
        // so it uses the raw completed-lap difference instead.
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 10, best: -1f, last: -1f, f2: 0f),
            Car(1, position: 2, classPosition: 2, lapsCompleted: 8, best: -1f, last: -1f, f2: 40f));

        var down = StandingsCalculator.Compute(snapshot, Roster((0, "GT3"), (1, "GT3")))[0].Rows[1];

        Assert.Equal(2, down.GapLapsDown);
    }

    [Fact]
    public void Compute_Interval_IsGapToTheCarDirectlyAhead()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        var gt3 = groups[0].Rows;
        Assert.Null(gt3[0].IntervalSeconds);                       // leader has no car ahead
        Assert.Equal(5.0, gt3[1].IntervalSeconds!.Value, 3);       // 5.0 - 0.0
        Assert.Equal(90.0, gt3[2].IntervalSeconds!.Value, 3);      // 95.0 - 5.0
        Assert.Equal(1, gt3[2].IntervalLapsDown);                  // 90 s over a ~90 s lap -> a lap
    }

    [Fact]
    public void Compute_LastDelta_IsLastMinusBest()
    {
        var leader = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster())[0].Rows[0];

        // car0: last 90.5, best 90.0 -> +0.5
        Assert.Equal(0.5, leader.LastDeltaSeconds!.Value, 3);
    }

    [Fact]
    public void Compute_LastDelta_NullWhenBestOrLastUnknown()
    {
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 5, best: 90f, last: -1f, f2: 0f));

        Assert.Null(StandingsCalculator.Compute(snapshot, Roster((0, "GT3")))[0].Rows[0].LastDeltaSeconds);
    }

    [Fact]
    public void Compute_PopulatesStrengthOfFieldPerClass()
    {
        // Every roster driver is iR 2000, so each class's SoF is 2000.
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        Assert.All(groups, g => Assert.Equal(2000, g.StrengthOfField));
    }

    [Fact]
    public void Compute_PopulatesBestAndLastLap()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        var leader = groups[0].Rows[0];
        Assert.Equal(90.0, leader.BestLapSeconds!.Value, 3);
        Assert.Equal(90.5, leader.LastLapSeconds!.Value, 3);
    }

    [Fact]
    public void Compute_NonPositiveLapTimes_BecomeNull()
    {
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 0, best: -1f, last: 0f, f2: 0f));

        var row = StandingsCalculator.Compute(snapshot, Roster((0, "GT3")))[0].Rows[0];

        Assert.Null(row.BestLapSeconds);
        Assert.Null(row.LastLapSeconds);
    }

    [Fact]
    public void Compute_FlagsThePlayerRow()
    {
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(playerCarIdx: 1), MulticlassRoster());

        var allRows = groups.SelectMany(g => g.Rows).ToList();
        Assert.Single(allRows, r => r.IsPlayer);
        Assert.True(Assert.Single(allRows, r => r.CarIdx == 1).IsPlayer);
    }

    [Fact]
    public void Compute_FlagsTheSingleFastestBestLapAcrossTheField()
    {
        // car0's 90.0 is the fastest valid best lap of the whole (two-class) field.
        var groups = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster());

        var allRows = groups.SelectMany(g => g.Rows).ToList();
        Assert.Single(allRows, r => r.IsSessionBestLap);
        Assert.True(Assert.Single(allRows, r => r.CarIdx == 0).IsSessionBestLap);
    }

    [Fact]
    public void Compute_MaxPerClass_TruncatesButAlwaysKeepsPlayer()
    {
        // Player is the 3rd GT3 car (idx 2), outside a top-1 window.
        var snapshot = MulticlassSnapshot(playerCarIdx: 2);

        var gt3 = StandingsCalculator.Compute(snapshot, MulticlassRoster(), maxPerClass: 1)[0];

        Assert.Equal(2, gt3.Rows.Count);
        Assert.True(gt3.Rows[0].IsClassLeader);          // leader kept
        Assert.Equal(2, gt3.Rows[1].CarIdx);             // player appended
        Assert.True(gt3.Rows[1].IsPlayer);
    }

    [Fact]
    public void Compute_WithoutMetadata_SingleGroupWithFallbackNames()
    {
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 5, best: 90f, last: 90f, f2: 0f),
            Car(7, position: 2, classPosition: 2, lapsCompleted: 5, best: 91f, last: 91f, f2: 3f));

        var groups = StandingsCalculator.Compute(snapshot, metadata: null);

        var group = Assert.Single(groups);
        Assert.Equal(string.Empty, group.ClassShortName);
        Assert.Equal("Car 7", Assert.Single(group.Rows, r => r.CarIdx == 7).DisplayName);
    }

    [Fact]
    public void Compute_ExcludesNotInWorldAndNonRosterCars()
    {
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 5, best: 90f, last: 90f, f2: 0f),
            Car(1, position: 2, classPosition: 2, lapsCompleted: 5, best: 91f, last: 91f, f2: 3f,
                surface: CarTrackSurface.NotInWorld),
            Car(9, position: 3, classPosition: 3, lapsCompleted: 5, best: 92f, last: 92f, f2: 6f)); // not in roster

        var rows = StandingsCalculator.Compute(snapshot, Roster((0, "GT3"), (1, "GT3")))
            .SelectMany(g => g.Rows)
            .ToList();

        Assert.DoesNotContain(rows, r => r.CarIdx == 1); // not in world
        Assert.DoesNotContain(rows, r => r.CarIdx == 9); // not in roster
        Assert.Single(rows);
    }

    [Fact]
    public void Compute_EmptyField_ReturnsEmpty()
    {
        var snapshot = Snapshot(playerCarIdx: 0);

        Assert.Empty(StandingsCalculator.Compute(snapshot, MulticlassRoster()));
    }

    [Fact]
    public void Compute_ResolvesManufacturerFromCarPath()
    {
        var snapshot = Snapshot(
            playerCarIdx: 0,
            Car(0, position: 1, classPosition: 1, lapsCompleted: 5, best: 90f, last: 90f, f2: 0f),
            Car(1, position: 2, classPosition: 2, lapsCompleted: 5, best: 91f, last: 91f, f2: 3f));

        var drivers = new Dictionary<int, RosterDriver>
        {
            [0] = new(0, "Driver 0", "0", IRating: 2000, License: "A 4.99",
                ClassEstLapTimeSeconds: 90f, ClassShortName: "GT3", ClassColorRaw: "16750899",
                CarPath: "ferrari296gt3"),
            // No CarPath (older build / missing) must degrade to Unknown, not throw.
            [1] = new(1, "Driver 1", "1", IRating: 2000, License: "A 4.99",
                ClassEstLapTimeSeconds: 90f, ClassShortName: "GT3", ClassColorRaw: "16750899"),
        };
        var metadata = new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [0] = "Race" },
            PlayerSetupName: "race_setup.sto",
            PlayerSetupIsModified: false);

        var rows = StandingsCalculator.Compute(snapshot, metadata).SelectMany(g => g.Rows).ToList();

        Assert.Equal(Manufacturer.Ferrari, rows.Single(r => r.CarIdx == 0).Manufacturer);
        Assert.Equal(Manufacturer.Unknown, rows.Single(r => r.CarIdx == 1).Manufacturer);
    }

    [Fact]
    public void Compute_PositionsGained_IsStartMinusCurrentClassPosition()
    {
        // GT3: car0 started 3rd and now leads (+2); car2 started 1st and is now
        // 3rd (-2); car1 hasn't moved.
        var startPositions = new Dictionary<int, int> { [0] = 3, [1] = 2, [2] = 1 };

        var gt3 = StandingsCalculator.Compute(
            MulticlassSnapshot(), MulticlassRoster(), maxPerClass: 30, startPositions)[0];

        Assert.Equal(2, Assert.Single(gt3.Rows, r => r.CarIdx == 0).PositionsGained);
        Assert.Equal(0, Assert.Single(gt3.Rows, r => r.CarIdx == 1).PositionsGained);
        Assert.Equal(-2, Assert.Single(gt3.Rows, r => r.CarIdx == 2).PositionsGained);
    }

    [Fact]
    public void Compute_PositionsGained_IsNullWithoutAStartingPosition()
    {
        // No start positions at all (outside a race), and a car missing from an
        // otherwise-populated map, both mean "unknown" rather than "gained none".
        var noneAtAll = StandingsCalculator.Compute(MulticlassSnapshot(), MulticlassRoster())[0];
        Assert.All(noneAtAll.Rows, r => Assert.Null(r.PositionsGained));

        var partial = StandingsCalculator.Compute(
            MulticlassSnapshot(), MulticlassRoster(), maxPerClass: 30,
            new Dictionary<int, int> { [0] = 1 })[0];

        Assert.Equal(0, Assert.Single(partial.Rows, r => r.CarIdx == 0).PositionsGained);
        Assert.Null(Assert.Single(partial.Rows, r => r.CarIdx == 1).PositionsGained);
    }

    private static CarTelemetry Car(
        int idx, int position, int classPosition, int lapsCompleted,
        float best, float last, float f2,
        bool onPitRoad = false, CarTrackSurface surface = CarTrackSurface.OnTrack) =>
        new(idx, Lap: lapsCompleted + 1, LapDistPct: 0f, EstTimeSeconds: 0f, onPitRoad, surface,
            Position: position, ClassPosition: classPosition, LapsCompleted: lapsCompleted,
            BestLapTimeSeconds: best, LastLapTimeSeconds: last, F2TimeSeconds: f2);

    private static TelemetrySnapshot Snapshot(int playerCarIdx, params CarTelemetry[] cars) => new()
    {
        SessionTimeSeconds = 0,
        SessionNum = 0,
        SessionTimeRemainSeconds = 0,
        SessionLapsRemain = -1,
        Lap = 10,
        FuelLevelLiters = 40f,
        SpeedMetersPerSecond = 50f,
        Gear = 4,
        IsOnTrack = true,
        PlayerCarIdx = playerCarIdx,
        AirTempC = 25f,
        TrackTempC = 40f,
        Wetness = TrackWetness.Dry,
        BrakeBiasPct = 0f,
        IncidentCount = 0,
        CarLeftRight = CarLeftRight.Clear,
        Cars = cars,
    };

    private static SessionMetadata Roster(params (int Idx, string Class)[] entries)
    {
        var colors = new Dictionary<string, string> { ["GT3"] = "16750899", ["GTE"] = "3852397" };
        var drivers = entries.ToDictionary(
            e => e.Idx,
            e => new RosterDriver(
                e.Idx,
                $"Driver {e.Idx}",
                e.Idx.ToString(),
                IRating: 2000,
                License: "A 4.99",
                ClassEstLapTimeSeconds: 90f,
                ClassShortName: e.Class,
                ClassColorRaw: colors.GetValueOrDefault(e.Class)));

        return new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [0] = "Race" },
            PlayerSetupName: "race_setup.sto",
            PlayerSetupIsModified: false);
    }
}
