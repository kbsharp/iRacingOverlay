using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Rating;

/// <summary>
/// Like the iRating tracker's tests, these read as sessions rather than as unit
/// tests: the tracker's job is surviving a real weekend, including the parts
/// that are not a race.
/// </summary>
public class SafetyTrackerTests
{
    private const int Player = 0;
    private const int Turns = 10;

    private static SessionMetadata Metadata(string sessionType = "Race", int turns = Turns) =>
        new(
            new Dictionary<int, RosterDriver>
            {
                [Player] = new(Player, "Driver", "1", 2000, "A 4.99", 90f, "GT3", null),
            },
            new Dictionary<int, string> { [0] = sessionType, [1] = sessionType },
            PlayerSetupName: "race.sto",
            PlayerSetupIsModified: false,
            TrackNumTurns: turns);

    private static TelemetrySnapshot Snapshot(int lapsCompleted, int incidents, int sessionNum = 0) => new()
    {
        SessionTimeSeconds = 600,
        SessionNum = sessionNum,
        SessionTimeRemainSeconds = 600,
        SessionLapsRemain = -1,
        Lap = lapsCompleted + 1,
        FuelLevelLiters = 40f,
        SpeedMetersPerSecond = 50f,
        Gear = 4,
        IsOnTrack = true,
        PlayerCarIdx = Player,
        AirTempC = 25f,
        TrackTempC = 40f,
        Wetness = TrackWetness.Dry,
        BrakeBiasPct = 0f,
        IncidentCount = incidents,
        Flags = SessionFlags.Green,
        CarLeftRight = CarLeftRight.Clear,
        Cars =
        [
            new CarTelemetry(
                CarIdx: Player,
                Lap: lapsCompleted + 1,
                LapDistPct: 0.5f,
                EstTimeSeconds: 45f,
                OnPitRoad: false,
                Surface: CarTrackSurface.OnTrack,
                Position: 1,
                ClassPosition: 1,
                LapsCompleted: lapsCompleted,
                BestLapTimeSeconds: 90f,
                LastLapTimeSeconds: 90.5f,
                F2TimeSeconds: 0f),
        ],
    };

    [Fact]
    public void ShowsNothingBeforeEnoughLaps()
    {
        var tracker = new SafetyTracker();

        var outlook = tracker.Update(Snapshot(lapsCompleted: 1, incidents: 0), Metadata());

        Assert.False(outlook.HasValue);
        Assert.Equal(SafetyOutlookState.Pending, outlook.State);
    }

    [Fact]
    public void ReportsCornersPerIncidentOnceRunning()
    {
        var tracker = new SafetyTracker();

        var outlook = tracker.Update(Snapshot(lapsCompleted: 10, incidents: 4), Metadata());

        Assert.True(outlook.HasValue);
        Assert.Equal(100, outlook.Corners);      // 10 turns x 10 laps
        Assert.Equal(25, outlook.SessionCpi);    // 100 corners / 4x
    }

    [Fact]
    public void CleanSessionHasNoFiniteRateAndAlwaysReadsAsGaining()
    {
        var tracker = new SafetyTracker(new CpiHistory(1000, 10));

        var outlook = tracker.Update(Snapshot(lapsCompleted: 10, incidents: 0), Metadata());

        Assert.True(outlook.IsClean);
        Assert.Equal(double.PositiveInfinity, outlook.SessionCpi);
        Assert.Equal(RatingTrend.Up, outlook.Trend);
    }

    [Fact]
    public void BeatingTheBaselineReadsAsGaining()
    {
        // Baseline CPI 50; this session is running at 100.
        var tracker = new SafetyTracker(new CpiHistory(1000, 20));

        var outlook = tracker.Update(Snapshot(lapsCompleted: 20, incidents: 2), Metadata());

        Assert.Equal(100, outlook.SessionCpi);
        Assert.Equal(50, outlook.BaselineCpi);
        Assert.Equal(RatingTrend.Up, outlook.Trend);
    }

    [Fact]
    public void FallingShortOfTheBaselineReadsAsLosing()
    {
        var tracker = new SafetyTracker(new CpiHistory(1000, 20));   // CPI 50

        var outlook = tracker.Update(Snapshot(lapsCompleted: 20, incidents: 10), Metadata());

        Assert.Equal(20, outlook.SessionCpi);
        Assert.Equal(RatingTrend.Down, outlook.Trend);
    }

    /// <summary>
    /// A brand-new install has no baseline. The CPI figure is still a real
    /// measurement, but the direction is unknown and must not be asserted.
    /// </summary>
    [Fact]
    public void WithoutABaselineThereIsAFigureButNoDirection()
    {
        var tracker = new SafetyTracker();

        var outlook = tracker.Update(Snapshot(lapsCompleted: 10, incidents: 2), Metadata());

        Assert.True(outlook.HasValue);
        Assert.False(outlook.HasTrend);
        Assert.Equal(RatingTrend.Flat, outlook.Trend);
    }

    [Fact]
    public void HidesItselfWhenTheSimReportsNoCornerCount()
    {
        var tracker = new SafetyTracker();

        var outlook = tracker.Update(Snapshot(lapsCompleted: 10, incidents: 2), Metadata(turns: 0));

        Assert.False(outlook.HasValue);
        Assert.Equal(SafetyOutlookState.Unavailable, outlook.State);
    }

    [Fact]
    public void HidesItselfWithNoMetadataAtAll()
    {
        var tracker = new SafetyTracker();

        Assert.False(tracker.Update(Snapshot(lapsCompleted: 10, incidents: 2), metadata: null).HasValue);
    }

    /// <summary>
    /// Offline testing moves no rating. Counting it would pour hours of clean
    /// corners into the baseline and make every real session look reckless.
    /// </summary>
    [Fact]
    public void OfflineTestingIsIgnoredEntirely()
    {
        var tracker = new SafetyTracker();
        var testing = Metadata("Offline Testing");

        var outlook = tracker.Update(Snapshot(lapsCompleted: 40, incidents: 0), testing);

        Assert.False(outlook.HasValue);

        tracker.CommitSession();
        Assert.Equal(CpiHistory.Empty, tracker.History);
    }

    [Fact]
    public void PracticeCountsBecauseItMovesSafetyRatingToo()
    {
        var tracker = new SafetyTracker();

        Assert.True(tracker.Update(Snapshot(lapsCompleted: 10, incidents: 1), Metadata("Practice")).HasValue);
    }

    [Fact]
    public void SessionIsBankedWhenTheSimMovesOn()
    {
        var tracker = new SafetyTracker();
        tracker.Update(Snapshot(lapsCompleted: 30, incidents: 3), Metadata());

        // Qualifying starts: the practice session just ended is now history.
        tracker.Update(Snapshot(lapsCompleted: 0, incidents: 0, sessionNum: 1), Metadata());

        Assert.Equal(300, tracker.History.Corners);
        Assert.Equal(3, tracker.History.IncidentPoints);
    }

    [Fact]
    public void SessionInProgressIsNotYetPartOfItsOwnBaseline()
    {
        var tracker = new SafetyTracker();

        var outlook = tracker.Update(Snapshot(lapsCompleted: 40, incidents: 4), Metadata());

        Assert.Null(outlook.BaselineCpi);
        Assert.Equal(CpiHistory.Empty, tracker.History);
    }

    /// <summary>
    /// The sim reports a car that has left the world with a negative lap count,
    /// and the incident count can reset as a session tears down. Neither may
    /// rewind a session that has already been driven.
    /// </summary>
    [Fact]
    public void LeavingTheWorldDoesNotEraseTheSessionSoFar()
    {
        var tracker = new SafetyTracker();
        tracker.Update(Snapshot(lapsCompleted: 30, incidents: 5), Metadata());

        var outlook = tracker.Update(Snapshot(lapsCompleted: -1, incidents: 0), Metadata());

        Assert.Equal(300, outlook.Corners);
        Assert.Equal(5, outlook.IncidentPoints);
    }

    /// <summary>
    /// Both session strips share one tracker and both feed it every frame, so
    /// updating twice with the same snapshot must change nothing.
    /// </summary>
    [Fact]
    public void RepeatingAFrameDoesNotDoubleCount()
    {
        var tracker = new SafetyTracker();
        var snapshot = Snapshot(lapsCompleted: 30, incidents: 3);
        var metadata = Metadata();

        tracker.Update(snapshot, metadata);
        var second = tracker.Update(snapshot, metadata);

        Assert.Equal(300, second.Corners);
        Assert.Equal(3, second.IncidentPoints);

        tracker.CommitSession();
        Assert.Equal(300, tracker.History.Corners);
    }

    [Fact]
    public void CommittingTwiceDoesNotBankTheSameSessionTwice()
    {
        var tracker = new SafetyTracker();
        tracker.Update(Snapshot(lapsCompleted: 30, incidents: 3), Metadata());

        tracker.CommitSession();
        tracker.CommitSession();

        Assert.Equal(300, tracker.History.Corners);
        Assert.Equal(3, tracker.History.IncidentPoints);
    }
}
