using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Tests.Fuel;

public class LapTimeTrackerTests
{
    [Fact]
    public void Average_BeforeAnyCompletedLap_IsNull()
    {
        var tracker = new LapTimeTracker();

        tracker.Update(lap: 1, sessionTimeSeconds: 10);

        Assert.Null(tracker.AverageLapTimeSeconds);
        Assert.Null(tracker.LastLapTimeSeconds);
    }

    [Fact]
    public void Update_AfterOneLap_MeasuresItsDuration()
    {
        var tracker = new LapTimeTracker();

        tracker.Update(1, 10);
        tracker.Update(2, 100);

        Assert.Equal(90.0, tracker.AverageLapTimeSeconds!.Value, 3);
        Assert.Equal(90.0, tracker.LastLapTimeSeconds!.Value, 3);
    }

    [Fact]
    public void Update_AveragesTheMostRecentWindowOfLaps()
    {
        var tracker = new LapTimeTracker(windowSize: 2);

        tracker.Update(1, 0);
        tracker.Update(2, 100); // 100 s - falls out of the window
        tracker.Update(3, 190); // 90 s
        tracker.Update(4, 270); // 80 s

        Assert.Equal(85.0, tracker.AverageLapTimeSeconds!.Value, 3);
    }

    [Fact]
    public void Update_LapCounterJumpingByMoreThanOne_IsNotMeasured()
    {
        var tracker = new LapTimeTracker();

        tracker.Update(1, 0);
        tracker.Update(3, 180); // +2 laps - interval isn't a single lap
        tracker.Update(4, 270); // clean lap

        Assert.Equal(90.0, tracker.AverageLapTimeSeconds!.Value, 3);
    }

    [Fact]
    public void Update_LapCounterGoingBackwards_RebaselinesWithoutRecording()
    {
        var tracker = new LapTimeTracker();

        tracker.Update(5, 400);
        tracker.Update(6, 490);   // 90 s recorded
        tracker.Update(2, 100);   // restart - re-baseline
        tracker.Update(3, 180);   // 80 s recorded

        Assert.Equal(85.0, tracker.AverageLapTimeSeconds!.Value, 3);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        var tracker = new LapTimeTracker();
        tracker.Update(1, 0);
        tracker.Update(2, 90);

        tracker.Reset();

        Assert.Null(tracker.AverageLapTimeSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WindowSizeBelowOne_Throws(int windowSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LapTimeTracker(windowSize));
    }
}
