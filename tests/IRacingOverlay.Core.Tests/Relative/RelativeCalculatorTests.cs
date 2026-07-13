using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Relative;

public class RelativeCalculatorTests
{
    private const int PlayerIdx = 0;
    private const float LapTimeSeconds = 90f;

    [Fact]
    public void Compute_PlayerNotInWorld_ReturnsEmpty()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.5f, surface: CarTrackSurface.NotInWorld),
            Car(1, pct: 0.6f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.Empty(rows);
    }

    [Fact]
    public void Compute_OrdersNearestCarsFarthestAheadFirst()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.50f),
            Car(1, pct: 0.52f),
            Car(2, pct: 0.60f),
            Car(3, pct: 0.70f),
            Car(4, pct: 0.80f), // 4th car ahead - should be dropped
            Car(5, pct: 0.45f),
            Car(6, pct: 0.40f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.Equal(new[] { 3, 2, 1, PlayerIdx, 5, 6 }, rows.Select(r => r.CarIdx));
        Assert.True(rows[3].IsPlayer);
        Assert.All(rows.Take(3), r => Assert.True(r.DeltaSeconds > 0));
        Assert.All(rows.Skip(4), r => Assert.True(r.DeltaSeconds < 0));
    }

    [Fact]
    public void Compute_PlayerRow_HasZeroDelta()
    {
        var snapshot = Snapshot(Car(PlayerIdx, pct: 0.3f), Car(1, pct: 0.4f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        var player = Assert.Single(rows, r => r.IsPlayer);
        Assert.Equal(0, player.DeltaSeconds);
    }

    [Fact]
    public void Compute_CarJustAcrossStartFinishAhead_GetsSmallPositiveDelta()
    {
        // Player near the line, other car just across it: raw EstTime delta is
        // hugely negative and must be wrapped forward by one lap.
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.95f),
            Car(1, pct: 0.05f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        var other = Assert.Single(rows, r => r.CarIdx == 1);
        Assert.Equal(9.0, other.DeltaSeconds, 1);

        var order = rows.Select(r => r.CarIdx).ToList();
        Assert.True(order.IndexOf(1) < order.IndexOf(PlayerIdx), "wrapped car should appear above the player");
    }

    [Fact]
    public void Compute_CarJustBehindAcrossStartFinish_GetsSmallNegativeDelta()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.05f),
            Car(1, pct: 0.95f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        var other = Assert.Single(rows, r => r.CarIdx == 1);
        Assert.Equal(-9.0, other.DeltaSeconds, 1);
    }

    [Fact]
    public void Compute_ExcludesCarsNotInWorld()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.5f),
            Car(1, pct: 0.6f, surface: CarTrackSurface.NotInWorld));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.DoesNotContain(rows, r => r.CarIdx == 1);
    }

    [Fact]
    public void Compute_ExcludesCarsMissingFromRoster()
    {
        // Pace cars and spectators are filtered out of the roster upstream.
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.5f),
            Car(1, pct: 0.6f),
            Car(9, pct: 0.55f)); // not in roster

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.DoesNotContain(rows, r => r.CarIdx == 9);
        Assert.Contains(rows, r => r.CarIdx == 1);
    }

    [Fact]
    public void Compute_ClassifiesLapAheadAndLapBehindCars()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, lap: 5, pct: 0.50f),
            Car(1, lap: 6, pct: 0.52f),  // one lap ahead
            Car(2, lap: 4, pct: 0.48f),  // one lap down
            Car(3, lap: 5, pct: 0.55f)); // same lap

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.Equal(LapDifference.LapAhead, Assert.Single(rows, r => r.CarIdx == 1).LapDifference);
        Assert.Equal(LapDifference.LapBehind, Assert.Single(rows, r => r.CarIdx == 2).LapDifference);
        Assert.Equal(LapDifference.SameLap, Assert.Single(rows, r => r.CarIdx == 3).LapDifference);
    }

    [Fact]
    public void Compute_CarCrossingLineJustAhead_IsStillSameLap()
    {
        // The car ahead has already crossed the line (lap counter +1) but is
        // physically right in front - it must not be classified a lap ahead.
        var snapshot = Snapshot(
            Car(PlayerIdx, lap: 5, pct: 0.99f),
            Car(1, lap: 6, pct: 0.01f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.Equal(LapDifference.SameLap, Assert.Single(rows, r => r.CarIdx == 1).LapDifference);
    }

    [Fact]
    public void Compute_FlagsCarsOnPitRoadOrInPitStalls()
    {
        var snapshot = Snapshot(
            Car(PlayerIdx, pct: 0.5f),
            Car(1, pct: 0.52f, onPitRoad: true),
            Car(2, pct: 0.48f, surface: CarTrackSurface.InPitStall));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        Assert.True(Assert.Single(rows, r => r.CarIdx == 1).InPits);
        Assert.True(Assert.Single(rows, r => r.CarIdx == 2).InPits);
        Assert.False(Assert.Single(rows, r => r.IsPlayer).InPits);
    }

    [Fact]
    public void Compute_UsesRosterForDisplayData()
    {
        var snapshot = Snapshot(Car(PlayerIdx, pct: 0.5f), Car(1, pct: 0.6f));

        var rows = RelativeCalculator.Compute(snapshot, Roster());

        var other = Assert.Single(rows, r => r.CarIdx == 1);
        Assert.Equal("Driver 1", other.DisplayName);
        Assert.Equal("1", other.CarNumber);
        Assert.Equal(2000, other.IRating);
        Assert.Equal(IRatingTier.Mid, other.IRatingTier);
        Assert.Equal("A 4.99", other.License);
        Assert.Equal(LicenseTier.A, other.LicenseTier);
        Assert.Equal("GT3", other.ClassShortName);
        Assert.Equal("#FF9933", other.ClassColorHex);
    }

    [Fact]
    public void Compute_WithoutMetadata_UsesFallbackNames()
    {
        var snapshot = Snapshot(Car(PlayerIdx, pct: 0.5f), Car(7, pct: 0.6f));

        var rows = RelativeCalculator.Compute(snapshot, metadata: null);

        var other = Assert.Single(rows, r => r.CarIdx == 7);
        Assert.Equal("Car 7", other.DisplayName);
    }

    private static CarTelemetry Car(
        int idx,
        int lap = 5,
        float pct = 0f,
        bool onPitRoad = false,
        CarTrackSurface surface = CarTrackSurface.OnTrack) =>
        new(idx, lap, pct, EstTimeSeconds: pct * LapTimeSeconds, onPitRoad, surface, Position: idx + 1);

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
        Cars = cars,
    };

    /// <summary>Roster for car indices 0-8; index 9 is intentionally absent.</summary>
    private static SessionMetadata Roster()
    {
        var drivers = new Dictionary<int, RosterDriver>();

        for (var idx = 0; idx <= 8; idx++)
        {
            drivers[idx] = new RosterDriver(
                idx,
                $"Driver {idx}",
                idx.ToString(),
                IRating: 2000,
                License: "A 4.99",
                ClassEstLapTimeSeconds: LapTimeSeconds,
                ClassShortName: "GT3",
                ClassColorRaw: "16750899");
        }

        return new SessionMetadata(
            drivers,
            new Dictionary<int, string> { [0] = "Race" },
            PlayerSetupName: "race_setup.sto",
            PlayerSetupIsModified: false);
    }
}
