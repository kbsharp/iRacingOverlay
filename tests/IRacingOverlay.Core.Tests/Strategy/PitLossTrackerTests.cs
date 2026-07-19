using IRacingOverlay.Core.Strategy;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Strategy;

public class PitLossTrackerTests
{
    [Fact]
    public void NoStopsSeen_IsNotLearned()
    {
        var tracker = new PitLossTracker();

        tracker.Update(Snapshot(OnTrack(1, f2: 10)));

        Assert.False(tracker.IsLearned);
        Assert.Null(tracker.MedianLossSeconds);
        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void OneStop_IsRecordedButNotYetTrusted()
    {
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 10, exitF2: 40);

        Assert.Equal(1, tracker.SampleCount);

        // One stop can be anyone's bad day - a queue, a slow release, damage.
        Assert.False(tracker.IsLearned);
        Assert.Null(tracker.MedianLossSeconds);
    }

    [Fact]
    public void ThreeStops_LearnsTheMedianLoss()
    {
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 10, exitF2: 38); // 28
        Stop(tracker, carIdx: 2, entryF2: 20, exitF2: 50); // 30
        Stop(tracker, carIdx: 3, entryF2: 5, exitF2: 34);  // 29

        Assert.True(tracker.IsLearned);
        Assert.Equal(29, tracker.MedianLossSeconds);
    }

    [Fact]
    public void EvenSampleCount_AveragesTheMiddlePair()
    {
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 0, exitF2: 20); // 20
        Stop(tracker, carIdx: 2, entryF2: 0, exitF2: 30); // 30
        Stop(tracker, carIdx: 3, entryF2: 0, exitF2: 40); // 40
        Stop(tracker, carIdx: 4, entryF2: 0, exitF2: 50); // 50

        Assert.Equal(35, tracker.MedianLossSeconds);
    }

    [Fact]
    public void MedianIgnoresOneRuinedStop()
    {
        // The whole reason for a median rather than a mean: a single stop that
        // went wrong must not move the figure the driver acts on.
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 0, exitF2: 28);
        Stop(tracker, carIdx: 2, entryF2: 0, exitF2: 29);
        Stop(tracker, carIdx: 3, entryF2: 0, exitF2: 110); // stuck behind another car

        Assert.Equal(29, tracker.MedianLossSeconds);
    }

    [Theory]
    [InlineData(2)]    // a drive-through, or a car that clipped the lane
    [InlineData(200)]  // damage repair, or sat out a session in the box
    public void ImplausibleStops_AreNotLearnedFrom(double lossSeconds)
    {
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 10, exitF2: 10 + lossSeconds);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void OnlyTheEntryValueIsUsed_NotF2WhileStillInTheLane()
    {
        // F2Time keeps climbing all the while the car is stationary. If a later
        // in-lane frame overwrote the entry value the measured loss would collapse
        // toward zero - the stop would look free.
        var tracker = new PitLossTracker();

        tracker.Update(Snapshot(OnTrack(1, f2: 10)));
        tracker.Update(Snapshot(InLane(1, f2: 12)));
        tracker.Update(Snapshot(InLane(1, f2: 25)));
        tracker.Update(Snapshot(InLane(1, f2: 38)));
        tracker.Update(Snapshot(OnTrack(1, f2: 40)));

        Assert.Equal(1, tracker.SampleCount);

        Stop(tracker, carIdx: 2, entryF2: 0, exitF2: 30);
        Stop(tracker, carIdx: 3, entryF2: 0, exitF2: 30);

        Assert.Equal(30, tracker.MedianLossSeconds);
    }

    [Fact]
    public void CarAlreadyInTheLaneAtFirstSight_IsNotMeasured()
    {
        // We never saw it enter, so there is no "before" - measuring from the
        // first in-lane frame would report a fraction of the real loss.
        var tracker = new PitLossTracker();

        tracker.Update(Snapshot(InLane(1, f2: 30)));
        tracker.Update(Snapshot(OnTrack(1, f2: 40)));

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void CarLeavingTheWorld_IsNotMistakenForLeavingTheLane()
    {
        // A disconnect mid-stop must not bank a half-measured stop.
        var tracker = new PitLossTracker();

        tracker.Update(Snapshot(OnTrack(1, f2: 10)));
        tracker.Update(Snapshot(InLane(1, f2: 12)));
        tracker.Update(Snapshot(Car(1, f2: 30, surface: CarTrackSurface.NotInWorld, onPitRoad: false)));

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void SessionChange_ForgetsEverything()
    {
        // A practice lane and a race lane are not the same measurement.
        var tracker = new PitLossTracker();

        Stop(tracker, carIdx: 1, entryF2: 0, exitF2: 28);
        Stop(tracker, carIdx: 2, entryF2: 0, exitF2: 29);
        Stop(tracker, carIdx: 3, entryF2: 0, exitF2: 30);
        Assert.True(tracker.IsLearned);

        tracker.Update(Snapshot(sessionNum: 2, cars: OnTrack(1, f2: 10)));

        Assert.False(tracker.IsLearned);
        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void OnlyTheRecentPastIsKept()
    {
        // The lane gets slower in the wet and quicker as a race dries out; a
        // stop from an hour ago should not still be voting.
        var tracker = new PitLossTracker();

        for (var i = 0; i < 12; i++)
        {
            Stop(tracker, carIdx: i, entryF2: 0, exitF2: 20);
        }

        Assert.Equal(20, tracker.MedianLossSeconds);

        for (var i = 0; i < 12; i++)
        {
            Stop(tracker, carIdx: 100 + i, entryF2: 0, exitF2: 40);
        }

        Assert.Equal(12, tracker.SampleCount);
        Assert.Equal(40, tracker.MedianLossSeconds);
    }

    private static void Stop(PitLossTracker tracker, int carIdx, double entryF2, double exitF2)
    {
        tracker.Update(Snapshot(OnTrack(carIdx, entryF2)));
        tracker.Update(Snapshot(InLane(carIdx, entryF2)));
        tracker.Update(Snapshot(OnTrack(carIdx, exitF2)));
    }

    private static CarTelemetry OnTrack(int carIdx, double f2) =>
        Car(carIdx, f2, CarTrackSurface.OnTrack, onPitRoad: false);

    private static CarTelemetry InLane(int carIdx, double f2) =>
        Car(carIdx, f2, CarTrackSurface.InPitStall, onPitRoad: true);

    private static CarTelemetry Car(
        int carIdx, double f2, CarTrackSurface surface, bool onPitRoad) =>
        new(
            CarIdx: carIdx,
            Lap: 1,
            LapDistPct: 0.5f,
            EstTimeSeconds: 0,
            OnPitRoad: onPitRoad,
            Surface: surface,
            Position: 1,
            ClassPosition: 1,
            LapsCompleted: 1,
            BestLapTimeSeconds: 90,
            LastLapTimeSeconds: 90,
            F2TimeSeconds: (float)f2);

    private static TelemetrySnapshot Snapshot(params CarTelemetry[] cars) =>
        Snapshot(sessionNum: 1, cars);

    private static TelemetrySnapshot Snapshot(int sessionNum, params CarTelemetry[] cars) =>
        new()
        {
            SessionTimeSeconds = 0,
            SessionNum = sessionNum,
            SessionTimeRemainSeconds = 1800,
            SessionLapsRemain = 20,
            Lap = 1,
            FuelLevelLiters = 50,
            SpeedMetersPerSecond = 50,
            Gear = 4,
            IsOnTrack = true,
            PlayerCarIdx = 0,
            AirTempC = 20,
            TrackTempC = 30,
            Wetness = TrackWetness.Dry,
            BrakeBiasPct = 52,
            IncidentCount = 0,
            CarLeftRight = CarLeftRight.Clear,
            Cars = cars,
        };
}
