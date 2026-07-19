using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Relative;

public class PaceTrendTrackerTests
{
    private const int PlayerIdx = 0;
    private const int RivalIdx = 1;
    private const float LapTimeSeconds = 100f;

    [Fact]
    public void Update_TooFewSamples_ReportsNothing()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.02 * t), seconds: 3, stepSeconds: 1);

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_SamplesOverTooShortASpan_ReportsNothing()
    {
        var tracker = new PaceTrendTracker();

        // Plenty of samples, but only a few seconds of them.
        Feed(tracker, gapAt: t => 3.0 - (0.02 * t), seconds: 5, stepSeconds: 0.25);

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_GapShrinking_ReportsClosingRatePerLap()
    {
        var tracker = new PaceTrendTracker();

        // 0.004 s of gap per second, over a 100 s lap: 0.4 s/lap.
        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(PaceTrendDirection.Closing, trend.Direction);
        Assert.Equal(0.4, trend.RateSecondsPerLap, 3);
    }

    [Fact]
    public void Update_GapGrowing_ReportsPullingWithNegativeRate()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 + (0.004 * t), seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(PaceTrendDirection.Pulling, trend.Direction);
        Assert.Equal(-0.4, trend.RateSecondsPerLap, 3);
        Assert.Null(trend.LapsToContact);
    }

    [Fact]
    public void Update_GapSteady_ReportsHolding()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: _ => 3.0, seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(PaceTrendDirection.Holding, trend.Direction);
        Assert.Equal(0, trend.RateSecondsPerLap);
    }

    [Fact]
    public void Update_DriftInsideTheNoiseFloor_StillReadsAsHolding()
    {
        var tracker = new PaceTrendTracker();

        // 0.02 s/lap - real, but nothing a driver can act on.
        Feed(tracker, gapAt: t => 3.0 - (0.0002 * t), seconds: 30, stepSeconds: 1);

        Assert.Equal(PaceTrendDirection.Holding, tracker.For(RivalIdx).Direction);
    }

    [Fact]
    public void Update_Closing_ProjectsLapsUntilTheGapRunsOut()
    {
        var tracker = new PaceTrendTracker();

        // Ends at a 2.0 s gap closing 0.4 s/lap: five laps away.
        Feed(tracker, gapAt: t => 2.12 - (0.004 * t), seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(5.0, trend!.LapsToContact!.Value, 1);
    }

    [Fact]
    public void Update_ContactBeyondTheFlag_MarksItAsNotArriving()
    {
        var tracker = new PaceTrendTracker();

        // 2 s at 0.4 s/lap is five laps away; the race has three left.
        Feed(tracker, gapAt: t => 2.12 - (0.004 * t), seconds: 30, stepSeconds: 1, lapsRemaining: 3);

        Assert.False(tracker.For(RivalIdx).ArrivesBeforeFlag);
    }

    [Fact]
    public void Update_ContactInsideTheRemainingLaps_MarksItAsArriving()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 2.12 - (0.004 * t), seconds: 30, stepSeconds: 1, lapsRemaining: 10);

        Assert.True(tracker.For(RivalIdx).ArrivesBeforeFlag);
    }

    [Fact]
    public void Update_UnlimitedSession_LeavesArrivalUnknown()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 2.12 - (0.004 * t), seconds: 30, stepSeconds: 1);

        Assert.Null(tracker.For(RivalIdx).ArrivesBeforeFlag);
    }

    [Fact]
    public void Update_ClosingImpossiblySlowly_ReportsNoContactLap()
    {
        var tracker = new PaceTrendTracker();

        // A 5 s gap closing 0.01 s/lap is 500 laps away - arithmetic, not a forecast.
        Feed(tracker, gapAt: t => 5.0 - (0.0006 * t), seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(PaceTrendDirection.Closing, trend.Direction);
        Assert.Null(trend.LapsToContact);
    }

    [Fact]
    public void Update_GapJumps_DiscardsTheHistoryRatherThanRegressingAcrossIt()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);
        Assert.Equal(PaceTrendDirection.Closing, tracker.For(RivalIdx).Direction);

        // A tow drops them 20 s back in one frame.
        tracker.Update(Snapshot(31, gap: 23.0), Roster(), Rows(31, gap: 23.0));

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_CarEntersThePits_ForgetsIt()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);

        tracker.Update(Snapshot(31, gap: 2.88), Roster(), Rows(31, gap: 2.88, rivalInPits: true));

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_PlayerEntersThePits_ForgetsEveryone()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);

        tracker.Update(Snapshot(31, gap: 2.88), Roster(), Rows(31, gap: 2.88, playerInPits: true));

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_NewSession_StartsOver()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);

        tracker.Update(Snapshot(0, gap: 3.0, sessionNum: 2), Roster(), Rows(0, gap: 3.0));

        Assert.Equal(PaceTrend.None, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_SameFrameReplayed_DoesNotDisturbTheTrend()
    {
        var tracker = new PaceTrendTracker();

        Feed(tracker, gapAt: t => 3.0 - (0.004 * t), seconds: 30, stepSeconds: 1);
        var before = tracker.For(RivalIdx);

        // A settings change re-renders the last snapshot.
        tracker.Update(Snapshot(30, gap: 2.88), Roster(), Rows(30, gap: 2.88));

        Assert.Equal(before, tracker.For(RivalIdx));
    }

    [Fact]
    public void Update_CarBehind_IsTrackedByGapMagnitude()
    {
        var tracker = new PaceTrendTracker();

        // Negative delta: they are behind, and catching.
        Feed(tracker, gapAt: t => -(3.0 - (0.004 * t)), seconds: 30, stepSeconds: 1);

        var trend = tracker.For(RivalIdx);

        Assert.Equal(PaceTrendDirection.Closing, trend.Direction);
        Assert.Equal(0.4, trend.RateSecondsPerLap, 3);
    }

    [Fact]
    public void For_UnknownCar_ReportsNothing()
    {
        Assert.Equal(PaceTrend.None, new PaceTrendTracker().For(99));
    }

    private static void Feed(
        PaceTrendTracker tracker,
        Func<double, double> gapAt,
        double seconds,
        double stepSeconds,
        int lapsRemaining = -1)
    {
        for (var t = 0.0; t <= seconds; t += stepSeconds)
        {
            var gap = gapAt(t);
            tracker.Update(Snapshot(t, gap, lapsRemaining: lapsRemaining), Roster(), Rows(t, gap));
        }
    }

    private static IReadOnlyList<RelativeRow> Rows(
        double time,
        double gap,
        bool rivalInPits = false,
        bool playerInPits = false)
    {
        _ = time;

        return
        [
            Row(PlayerIdx, isPlayer: true, delta: 0, inPits: playerInPits),
            Row(RivalIdx, isPlayer: false, delta: gap, inPits: rivalInPits),
        ];
    }

    private static RelativeRow Row(int carIdx, bool isPlayer, double delta, bool inPits) =>
        new(
            CarIdx: carIdx,
            IsPlayer: isPlayer,
            Position: carIdx + 1,
            CarNumber: carIdx.ToString(),
            DisplayName: "Driver " + carIdx,
            License: "A 3.5",
            LicenseTier: Core.Formatting.LicenseTier.A,
            IRating: 2000,
            ClassShortName: "GT3",
            ClassColorHex: null,
            DeltaSeconds: delta,
            LapDifference: LapDifference.SameLap,
            InPits: inPits);

    private static TelemetrySnapshot Snapshot(
        double time,
        double gap,
        int sessionNum = 1,
        int lapsRemaining = -1)
    {
        _ = gap;

        return new TelemetrySnapshot
        {
            SessionTimeSeconds = time,
            SessionNum = sessionNum,
            SessionTimeRemainSeconds = 604800,
            SessionLapsRemain = lapsRemaining,
            Lap = 5,
            FuelLevelLiters = 40,
            SpeedMetersPerSecond = 60,
            Gear = 4,
            IsOnTrack = true,
            PlayerCarIdx = PlayerIdx,
            AirTempC = 20,
            TrackTempC = 30,
            Wetness = TrackWetness.Dry,
            BrakeBiasPct = 54,
            IncidentCount = 0,
            CarLeftRight = CarLeftRight.Clear,
            Cars = [],
        };
    }

    private static SessionMetadata Roster() =>
        new(
            DriversByCarIdx: new Dictionary<int, RosterDriver>
            {
                [PlayerIdx] = Driver(PlayerIdx),
                [RivalIdx] = Driver(RivalIdx),
            },
            SessionTypesByNum: new Dictionary<int, string>(),
            PlayerSetupName: "baseline",
            PlayerSetupIsModified: false);

    private static RosterDriver Driver(int carIdx) =>
        new(
            CarIdx: carIdx,
            DisplayName: "Driver " + carIdx,
            CarNumber: carIdx.ToString(),
            IRating: 2000,
            License: "A 3.5",
            ClassEstLapTimeSeconds: LapTimeSeconds,
            ClassShortName: "GT3",
            ClassColorRaw: null);
}
