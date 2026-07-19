using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Standings;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Standings;

public class StartPositionTrackerTests
{
    [Fact]
    public void Update_LatchesTheFirstPositionSeenInARace()
    {
        var tracker = new StartPositionTracker();

        tracker.Update(Snapshot(Car(0, classPosition: 3), Car(1, classPosition: 1)), RaceRoster());
        // The race runs on: car 0 climbs to the lead, car 1 drops to third.
        var start = tracker.Update(Snapshot(Car(0, classPosition: 1), Car(1, classPosition: 3)), RaceRoster());

        Assert.Equal(3, start[0]);
        Assert.Equal(1, start[1]);
    }

    [Fact]
    public void Update_OutsideARace_RecordsNothing()
    {
        var tracker = new StartPositionTracker();

        // Practice and qualifying order cars by lap time, so "places gained"
        // there would count improved laps rather than overtakes.
        var start = tracker.Update(Snapshot(Car(0, classPosition: 1)), Roster("Practice"));

        Assert.Empty(start);
    }

    [Fact]
    public void Update_QualifyingThenRace_BaselinesOnTheRace()
    {
        var tracker = new StartPositionTracker();

        tracker.Update(Snapshot(0, [Car(0, classPosition: 1)]), Roster("Qualify", sessionNum: 0));
        var start = tracker.Update(Snapshot(1, [Car(0, classPosition: 8)]), Roster("Race", sessionNum: 1));

        Assert.Equal(8, start[0]);
    }

    [Fact]
    public void Update_SessionChange_DropsThePreviousBaseline()
    {
        var tracker = new StartPositionTracker();

        tracker.Update(Snapshot(1, [Car(0, classPosition: 2)]), Roster("Race", sessionNum: 1));
        // A second race session (heat -> feature) starts its own count.
        var start = tracker.Update(Snapshot(2, [Car(0, classPosition: 5)]), Roster("Race", sessionNum: 2));

        Assert.Equal(5, start[0]);
    }

    [Fact]
    public void Update_UnclassifiedCar_IsLatchedOnceThePositionArrives()
    {
        var tracker = new StartPositionTracker();

        // iRacing reports position 0 before it has classified a car; recording
        // that would give the whole grid a bogus "gained 0" baseline.
        var first = tracker.Update(Snapshot(Car(0, classPosition: 0, position: 0)), RaceRoster());
        Assert.Empty(first);

        var start = tracker.Update(Snapshot(Car(0, classPosition: 4)), RaceRoster());
        Assert.Equal(4, start[0]);
    }

    [Fact]
    public void Update_FallsBackToOverallPositionWhenClassPositionIsUnset()
    {
        var tracker = new StartPositionTracker();

        var start = tracker.Update(Snapshot(Car(0, classPosition: 0, position: 6)), RaceRoster());

        Assert.Equal(6, start[0]);
    }

    [Fact]
    public void Update_CarNotInWorld_IsNotBaselinedUntilItAppears()
    {
        var tracker = new StartPositionTracker();

        tracker.Update(
            Snapshot(Car(0, classPosition: 1), Car(1, classPosition: 2, surface: CarTrackSurface.NotInWorld)),
            RaceRoster());
        var start = tracker.Update(Snapshot(Car(0, classPosition: 1), Car(1, classPosition: 9)), RaceRoster());

        // Car 1 spawned late, so its first real position is its baseline - better
        // than leaving it without an arrow for the rest of the race.
        Assert.Equal(9, start[1]);
    }

    [Fact]
    public void Update_ADisconnectedCarKeepsItsOriginalBaseline()
    {
        var tracker = new StartPositionTracker();

        tracker.Update(Snapshot(Car(0, classPosition: 2)), RaceRoster());
        tracker.Update(Snapshot(Car(0, classPosition: 2, surface: CarTrackSurface.NotInWorld)), RaceRoster());
        var start = tracker.Update(Snapshot(Car(0, classPosition: 7)), RaceRoster());

        Assert.Equal(2, start[0]);
    }

    private static CarTelemetry Car(
        int idx, int classPosition, int position = -1,
        CarTrackSurface surface = CarTrackSurface.OnTrack) =>
        new(idx, Lap: 1, LapDistPct: 0f, EstTimeSeconds: 0f, OnPitRoad: false, surface,
            Position: position < 0 ? classPosition : position,
            ClassPosition: classPosition, LapsCompleted: 0,
            BestLapTimeSeconds: -1f, LastLapTimeSeconds: -1f, F2TimeSeconds: 0f);

    private static TelemetrySnapshot Snapshot(params CarTelemetry[] cars) => Snapshot(0, cars);

    private static TelemetrySnapshot Snapshot(int sessionNum, CarTelemetry[] cars) => new()
    {
        SessionTimeSeconds = 0,
        SessionNum = sessionNum,
        SessionTimeRemainSeconds = 600,
        SessionLapsRemain = -1,
        Lap = 1,
        FuelLevelLiters = 40f,
        SpeedMetersPerSecond = 50f,
        Gear = 4,
        IsOnTrack = true,
        PlayerCarIdx = 0,
        AirTempC = 25f,
        TrackTempC = 40f,
        Wetness = TrackWetness.Dry,
        BrakeBiasPct = 0f,
        IncidentCount = 0,
        CarLeftRight = CarLeftRight.Clear,
        Cars = cars,
    };

    private static SessionMetadata RaceRoster() => Roster("Race");

    private static SessionMetadata Roster(string sessionType, int sessionNum = 0) => new(
        new Dictionary<int, RosterDriver>(),
        new Dictionary<int, string> { [sessionNum] = sessionType },
        PlayerSetupName: "race_setup.sto",
        PlayerSetupIsModified: false);
}
