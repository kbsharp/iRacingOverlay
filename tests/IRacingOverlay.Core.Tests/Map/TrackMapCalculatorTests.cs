using IRacingOverlay.Core.Map;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Map;

public class TrackMapCalculatorTests
{
    private const int PlayerIdx = 0;

    [Fact]
    public void Compute_DrawsTheWholeField_NotJustWhoIsNearby()
    {
        var snapshot = Snapshot(Car(PlayerIdx, 0.10f), Car(1, 0.40f), Car(2, 0.85f));

        var cars = TrackMapCalculator.Compute(snapshot, Roster());

        Assert.Equal(3, cars.Count);
        Assert.Equal([0.40, 0.85, 0.10], cars.Select(c => Math.Round(c.LapDistPct, 2)));
    }

    /// <summary>Your own mark is drawn last so it sits on top of whoever you are
    /// running alongside - the one car on the map you must never lose.</summary>
    [Fact]
    public void Compute_PutsThePlayerLast()
    {
        var cars = TrackMapCalculator.Compute(
            Snapshot(Car(1, 0.4f), Car(PlayerIdx, 0.1f), Car(2, 0.8f)), Roster());

        Assert.True(cars[^1].IsPlayer);
        Assert.Single(cars, c => c.IsPlayer);
    }

    [Fact]
    public void Compute_CarriesTheRostersNumberAndClassColour()
    {
        var cars = TrackMapCalculator.Compute(Snapshot(Car(PlayerIdx, 0.1f), Car(1, 0.4f)), Roster());

        var other = Assert.Single(cars, c => c.CarIdx == 1);
        Assert.Equal("1", other.CarNumber);
        Assert.Equal("#FF9933", other.ClassColorHex);
    }

    [Fact]
    public void Compute_CarsNotInWorld_AreExcluded()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, 0.1f),
            Car(1, 0.4f, surface: CarTrackSurface.NotInWorld));

        var cars = TrackMapCalculator.Compute(snapshot, Roster());

        Assert.Single(cars);
    }

    [Fact]
    public void Compute_CarsAbsentFromRoster_AreExcluded()
    {
        // Car 9 is not in the roster (pace car / spectator).
        var cars = TrackMapCalculator.Compute(Snapshot(Car(PlayerIdx, 0.1f), Car(9, 0.4f)), Roster());

        Assert.Single(cars);
    }

    /// <summary>Unlike the radar, the map keeps cars in the lane: where a rival is in
    /// their stop is a strategy question the map is glanced at to answer.</summary>
    [Fact]
    public void Compute_CarsInThePits_AreKeptAndFlagged()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, 0.1f),
            Car(1, 0.4f, onPitRoad: true),
            Car(2, 0.5f, surface: CarTrackSurface.InPitStall));

        var cars = TrackMapCalculator.Compute(snapshot, Roster());

        Assert.Equal(3, cars.Count);
        Assert.True(Assert.Single(cars, c => c.CarIdx == 1).InPits);
        Assert.True(Assert.Single(cars, c => c.CarIdx == 2).InPits);
        Assert.False(Assert.Single(cars, c => c.IsPlayer).InPits);
    }

    /// <summary>Before the session info arrives there is no roster to filter by; the
    /// map draws what telemetry reports rather than nothing at all.</summary>
    [Fact]
    public void Compute_NoMetadataYet_StillDrawsTheField()
    {
        var cars = TrackMapCalculator.Compute(Snapshot(Car(PlayerIdx, 0.1f), Car(9, 0.4f)), metadata: null);

        Assert.Equal(2, cars.Count);
        Assert.All(cars, c => Assert.Equal(string.Empty, c.CarNumber));
    }

    private static CarTelemetry Car(
        int idx,
        float pct,
        bool onPitRoad = false,
        CarTrackSurface surface = CarTrackSurface.OnTrack) =>
        new(idx, Lap: 5, pct, EstTimeSeconds: 0f, onPitRoad, surface, Position: idx + 1,
            ClassPosition: idx + 1, LapsCompleted: 4, BestLapTimeSeconds: 0f,
            LastLapTimeSeconds: 0f, F2TimeSeconds: 0f);

    private static TelemetrySnapshot Snapshot(params CarTelemetry[] cars) => new()
    {
        SessionTimeSeconds = 0,
        SessionNum = 0,
        SessionTimeRemainSeconds = 0,
        SessionLapsRemain = -1,
        Lap = 5,
        FuelLevelLiters = 40f,
        SpeedMetersPerSecond = 50f,
        Gear = 4,
        IsOnTrack = true,
        PlayerCarIdx = PlayerIdx,
        AirTempC = 25f,
        TrackTempC = 40f,
        Wetness = TrackWetness.Dry,
        BrakeBiasPct = 0f,
        IncidentCount = 0,
        CarLeftRight = CarLeftRight.Clear,
        Cars = cars,
    };

    /// <summary>Roster for car indices 0-8; index 9 is intentionally absent.</summary>
    private static SessionMetadata Roster()
    {
        var drivers = new Dictionary<int, RosterDriver>();
        for (var idx = 0; idx <= 8; idx++)
        {
            drivers[idx] = new RosterDriver(
                idx, $"Driver {idx}", idx.ToString(), IRating: 2000, License: "A 4.99",
                ClassEstLapTimeSeconds: 90f, ClassShortName: "GT3", ClassColorRaw: "16750899");
        }

        return new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [0] = "Race" },
            PlayerSetupName: "race_setup.sto",
            PlayerSetupIsModified: false);
    }
}
