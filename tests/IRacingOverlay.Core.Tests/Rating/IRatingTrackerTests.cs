using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Rating;

/// <summary>
/// These read as a race rather than as unit tests on purpose - the tracker's whole
/// job is surviving what actually happens over a race distance.
/// </summary>
public class IRatingTrackerTests
{
    private const int Player = 0;

    // A ten-car single-class field; the player starts and runs mid-pack.
    private static SessionMetadata Roster(
        string sessionType = "Race",
        int playerIRating = 2000,
        int fieldSize = 10,
        string playerClass = "GT3")
    {
        var drivers = Enumerable.Range(0, fieldSize).ToDictionary(
            i => i,
            i => new RosterDriver(
                i,
                $"Driver {i}",
                i.ToString(),
                IRating: i == Player ? playerIRating : 2000,
                License: "A 4.99",
                ClassEstLapTimeSeconds: 90f,
                ClassShortName: i == Player ? playerClass : "GT3",
                ClassColorRaw: "16750899"));

        return new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [0] = sessionType },
            PlayerSetupName: "race.sto",
            PlayerSetupIsModified: false);
    }

    private static CarTelemetry Car(
        int idx,
        int classPosition,
        int lapsCompleted,
        CarTrackSurface surface = CarTrackSurface.OnTrack) =>
        new(
            CarIdx: idx,
            Lap: lapsCompleted + 1,
            LapDistPct: 0.5f,
            EstTimeSeconds: 45f,
            OnPitRoad: false,
            Surface: surface,
            Position: classPosition,
            ClassPosition: classPosition,
            LapsCompleted: lapsCompleted,
            BestLapTimeSeconds: 90f,
            LastLapTimeSeconds: 90.5f,
            F2TimeSeconds: classPosition * 1.5f);

    private static TelemetrySnapshot Snapshot(
        IEnumerable<CarTelemetry> cars,
        SessionFlags flags = SessionFlags.Green,
        int sessionNum = 0) => new()
        {
            SessionTimeSeconds = 600,
            SessionNum = sessionNum,
            SessionTimeRemainSeconds = 600,
            SessionLapsRemain = -1,
            Lap = 5,
            FuelLevelLiters = 40f,
            SpeedMetersPerSecond = 50f,
            Gear = 4,
            IsOnTrack = true,
            PlayerCarIdx = Player,
            AirTempC = 25f,
            TrackTempC = 40f,
            Wetness = TrackWetness.Dry,
            BrakeBiasPct = 0f,
            IncidentCount = 0,
            Flags = flags,
            CarLeftRight = CarLeftRight.Clear,
            Cars = [.. cars],
        };

    /// <summary>A full field running, with the player at <paramref name="playerPosition"/>.</summary>
    private static TelemetrySnapshot Running(
        int playerPosition,
        int laps = 3,
        int fieldSize = 10,
        SessionFlags flags = SessionFlags.Green)
    {
        var cars = new List<CarTelemetry> { Car(Player, playerPosition, laps) };
        var position = 1;

        for (var idx = 1; idx < fieldSize; idx++)
        {
            if (position == playerPosition)
            {
                position++;
            }

            cars.Add(Car(idx, position++, laps));
        }

        return Snapshot(cars, flags);
    }

    [Fact]
    public void Update_PracticeSession_ShowsNothing()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 5), Roster(sessionType: "Practice"));

        Assert.Equal(IRatingProjectionState.Unavailable, projection.State);
        Assert.False(projection.HasValue);
    }

    [Fact]
    public void Update_QualifyingSession_ShowsNothing()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 1), Roster(sessionType: "Lone Qualify"));

        Assert.False(projection.HasValue);
    }

    [Fact]
    public void Update_BeforeTheFirstLapIsComplete_IsPendingRatherThanShown()
    {
        // On the grid the player's position is a qualifying result, not a race one.
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 8, laps: 0), Roster());

        Assert.Equal(IRatingProjectionState.Pending, projection.State);
        Assert.False(projection.HasValue);
    }

    [Fact]
    public void Update_AfterOneCompletedLap_GoesLive()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 5, laps: 1), Roster());

        Assert.Equal(IRatingProjectionState.Live, projection.State);
        Assert.True(projection.HasValue);
        Assert.Equal(5, projection.Position);
        Assert.Equal(10, projection.FieldSize);
    }

    [Fact]
    public void Update_LeadingAnEvenField_ProjectsAGain()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 1), Roster());

        Assert.True(projection.Delta > 0);
        Assert.Equal(2000 + projection.Delta, projection.Projected);
        Assert.Equal(2000, projection.Current);
    }

    [Fact]
    public void Update_LastInAnEvenField_ProjectsALoss()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 10), Roster());

        Assert.True(projection.Delta < 0);
        Assert.Equal(2000 + projection.Delta, projection.Projected);
    }

    [Fact]
    public void Update_ImprovingPosition_ImprovesTheProjection()
    {
        var tracker = new IRatingTracker();

        var backOfTheField = tracker.Update(Running(playerPosition: 9), Roster()).Delta;
        var afterOvertakes = tracker.Update(Running(playerPosition: 3), Roster()).Delta;

        Assert.True(afterOvertakes > backOfTheField);
    }

    [Fact]
    public void Update_OnlyRatesTheDriversInThePlayersOwnClass()
    {
        // A multiclass grid: the player is the sole "LMP2" entry among nine GT3s.
        var tracker = new IRatingTracker();
        var multiclass = Roster(playerClass: "LMP2");

        var projection = tracker.Update(Running(playerPosition: 5), multiclass);

        // One rated driver in the class is not a field - nothing to project against.
        Assert.False(projection.HasValue);
    }

    [Fact]
    public void Update_DriversWhoDisconnect_KeepCountingTowardsTheFieldSize()
    {
        // The heart of it. Three cars quit while the player runs fourth.
        //
        // Inheriting their positions is correct - iRacing classifies a DNF behind
        // anyone still circulating, so the player really does finish P1 and really
        // does score the P1 gain. What must NOT happen is the field shrinking to
        // seven: winning a 10-car race pays more than winning a 7-car one, and a
        // projection that quietly rebases on the survivors understates the result.
        var tracker = new IRatingTracker();

        var during = tracker.Update(Running(playerPosition: 4), Roster());
        Assert.Equal(10, during.FieldSize);
        Assert.Equal(4, during.Position);

        // Cars 1, 2 and 3 vanish; iRacing shuffles everyone else up a slot.
        var survivors = new List<CarTelemetry> { Car(Player, 1, 4) };
        for (var idx = 4; idx < 10; idx++)
        {
            survivors.Add(Car(idx, idx - 2, 4));
        }

        var after = tracker.Update(Snapshot(survivors), Roster());

        Assert.Equal(10, after.FieldSize);
        Assert.Equal(1, after.Position);
        Assert.True(after.Delta > during.Delta);

        // Priced as a win over the whole ten-car field, not over the seven left.
        Assert.Equal(IRatingCalculator.Delta(Enumerable.Repeat(2000, 10).ToArray(), 0, 1), after.Delta);
    }

    [Fact]
    public void Update_RetirementsAreOrderedByLapsCompleted()
    {
        var tracker = new IRatingTracker();

        // Everyone runs three laps; car 9 then peels off having done four, car 8
        // having done three. Both are behind the player, who keeps circulating.
        tracker.Update(Running(playerPosition: 5), Roster());

        var cars = new List<CarTelemetry> { Car(Player, 5, 5) };
        for (var idx = 1; idx < 8; idx++)
        {
            cars.Add(Car(idx, idx < 5 ? idx : idx + 1, 5));
        }

        cars.Add(Car(8, 9, 3, CarTrackSurface.NotInWorld));
        cars.Add(Car(9, 10, 4, CarTrackSurface.NotInWorld));

        var projection = tracker.Update(Snapshot(cars), Roster());

        // Both retirements sort behind the seven runners; the player keeps P5.
        Assert.Equal(10, projection.FieldSize);
        Assert.Equal(5, projection.Position);
    }

    [Fact]
    public void Update_UnderTheCheckeredButStillRunning_StaysLive()
    {
        // The leader has finished; the player is on their last tour and can still
        // gain or lose a place, so the number must keep moving.
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 4), Roster());

        var projection = tracker.Update(
            Running(playerPosition: 4, laps: 3, flags: SessionFlags.Checkered),
            Roster());

        Assert.Equal(IRatingProjectionState.Live, projection.State);
    }

    [Fact]
    public void Update_OnceThePlayerTakesTheFlag_TheValueIsCaptured()
    {
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 4), Roster());
        tracker.Update(Running(playerPosition: 4, laps: 3, flags: SessionFlags.Checkered), Roster());

        // Crossing the line under the checkered ends the player's race.
        var final = tracker.Update(
            Running(playerPosition: 4, laps: 4, flags: SessionFlags.Checkered),
            Roster());

        Assert.Equal(IRatingProjectionState.Final, final.State);
        Assert.True(final.HasValue);
    }

    [Fact]
    public void Update_AfterTheFlag_TheEmptyingGridCannotMoveTheNumber()
    {
        // The reason capture exists: within a minute of the flag most of the grid
        // has left. A live projection would drift all the way to a fantasy result.
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 8), Roster());
        tracker.Update(Running(playerPosition: 8, laps: 3, flags: SessionFlags.Checkered), Roster());

        var captured = tracker.Update(
            Running(playerPosition: 8, laps: 4, flags: SessionFlags.Checkered),
            Roster());

        Assert.Equal(IRatingProjectionState.Final, captured.State);

        // Everyone but the player disconnects.
        var alone = tracker.Update(
            Snapshot([Car(Player, 1, 4)], SessionFlags.Checkered),
            Roster());

        Assert.Equal(captured.Delta, alone.Delta);
        Assert.Equal(captured.Position, alone.Position);
        Assert.Equal(captured.FieldSize, alone.FieldSize);
    }

    [Fact]
    public void Update_PlayerLeavesTheWorldAfterTheFlag_AlsoCaptures()
    {
        // Towing to the paddock straight after the flag shouldn't lose the result.
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 6), Roster());
        tracker.Update(Running(playerPosition: 6, laps: 3, flags: SessionFlags.Checkered), Roster());

        var cars = new List<CarTelemetry> { Car(Player, 6, 3, CarTrackSurface.NotInWorld) };
        for (var idx = 1; idx < 10; idx++)
        {
            cars.Add(Car(idx, idx < 6 ? idx : idx + 1, 3));
        }

        var projection = tracker.Update(Snapshot(cars, SessionFlags.Checkered), Roster());

        Assert.Equal(IRatingProjectionState.Final, projection.State);
    }

    [Fact]
    public void Update_NewSessionNumber_StartsOver()
    {
        // Practice -> Qualify -> Race all arrive on the same telemetry stream.
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 1), Roster());
        tracker.Update(Running(playerPosition: 1, laps: 3, flags: SessionFlags.Checkered), Roster());
        tracker.Update(Running(playerPosition: 1, laps: 4, flags: SessionFlags.Checkered), Roster());
        Assert.Equal(IRatingProjectionState.Final, tracker.Current.State);

        var nextSession = Snapshot([.. Running(playerPosition: 1, laps: 0).Cars], sessionNum: 1);
        var projection = tracker.Update(nextSession, Roster());

        Assert.NotEqual(IRatingProjectionState.Final, projection.State);
    }

    [Fact]
    public void Update_PlayerWithNoIRating_ShowsNothing()
    {
        var tracker = new IRatingTracker();

        var projection = tracker.Update(Running(playerPosition: 3), Roster(playerIRating: 0));

        Assert.False(projection.HasValue);
    }

    [Fact]
    public void Update_WithoutSessionMetadata_ShowsNothing()
    {
        var tracker = new IRatingTracker();

        Assert.False(tracker.Update(Running(playerPosition: 3), metadata: null).HasValue);
    }

    [Fact]
    public void Update_IsStableAcrossRepeatedIdenticalFrames()
    {
        // 30 Hz of telemetry must not make the number twitch.
        var tracker = new IRatingTracker();
        var roster = Roster();

        var first = tracker.Update(Running(playerPosition: 6), roster);

        for (var i = 0; i < 50; i++)
        {
            var repeat = tracker.Update(Running(playerPosition: 6), roster);
            Assert.Equal(first.Delta, repeat.Delta);
        }
    }

    [Fact]
    public void Reset_ClearsAStickyFieldAndACapturedResult()
    {
        var tracker = new IRatingTracker();
        tracker.Update(Running(playerPosition: 2), Roster());

        tracker.Reset();

        Assert.Equal(IRatingProjectionState.Unavailable, tracker.Current.State);
    }
}
