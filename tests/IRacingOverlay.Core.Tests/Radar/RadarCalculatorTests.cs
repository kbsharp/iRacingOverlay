using IRacingOverlay.Core.Radar;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Radar;

public class RadarCalculatorTests
{
    private const int PlayerIdx = 0;
    private const double TrackLength = 1000.0;

    [Fact]
    public void Compute_MapNotReady_ReturnsEmptyNotReady()
    {
        var snapshot = Snapshot(Car(PlayerIdx, 0.5f), Car(1, 0.52f));

        var result = RadarCalculator.Compute(snapshot, Roster(), new TrackMap(100), TrackLength);

        Assert.False(result.MapReady);
        Assert.False(result.AnyInRange);
        Assert.Empty(result.Blips);
    }

    [Fact]
    public void Compute_ZeroTrackLength_ReturnsEmpty()
    {
        var snapshot = Snapshot(Car(PlayerIdx, 0.5f), Car(1, 0.52f));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), trackLengthMeters: 0);

        Assert.False(result.AnyInRange);
    }

    [Fact]
    public void Compute_CarWithinRange_ProducesBlipAheadWithRosterData()
    {
        // Car 20 m ahead on a straight.
        var snapshot = Snapshot(Car(PlayerIdx, 0.5f), Car(1, 0.52f));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), TrackLength);

        Assert.True(result.MapReady);
        var blip = Assert.Single(result.Blips);
        Assert.Equal(1, blip.CarIdx);
        Assert.Equal(20.0, blip.ForwardMeters, precision: 2);
        Assert.Equal(0.0, blip.RightMeters, precision: 2);
        Assert.Equal("1", blip.CarNumber);
        Assert.Equal("#FF9933", blip.ClassColorHex);
    }

    [Fact]
    public void Compute_CarBeyondRange_IsExcluded()
    {
        // 100 m ahead > 60 m default range.
        var snapshot = Snapshot(Car(PlayerIdx, 0.5f), Car(1, 0.6f));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), TrackLength);

        Assert.False(result.AnyInRange);
    }

    [Fact]
    public void Compute_CarsInPits_AreExcluded()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, 0.5f),
            Car(1, 0.52f, onPitRoad: true),
            Car(2, 0.48f, surface: CarTrackSurface.InPitStall));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), TrackLength);

        Assert.False(result.AnyInRange);
    }

    [Fact]
    public void Compute_CarsAbsentFromRoster_AreExcluded()
    {
        // Car 9 is not in the roster (pace car / spectator).
        var snapshot = Snapshot(Car(PlayerIdx, 0.5f), Car(9, 0.52f));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), TrackLength);

        Assert.False(result.AnyInRange);
    }

    [Fact]
    public void Compute_PlayerNotInWorld_ReturnsEmpty()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, 0.5f, surface: CarTrackSurface.NotInWorld),
            Car(1, 0.52f));

        var result = RadarCalculator.Compute(snapshot, Roster(), StraightMap(), TrackLength);

        Assert.False(result.AnyInRange);
    }

    private static TrackMap StraightMap(int buckets = 720)
    {
        var map = new TrackMap(buckets);
        for (var i = 0; i < buckets; i++)
        {
            map.Sample((i + 0.5) / buckets, headingRad: 0.0);
        }

        return map;
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
