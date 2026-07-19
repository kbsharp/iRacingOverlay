using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Relative;

/// <summary>
/// Builds the relative view: the cars physically nearest the player on
/// track, ordered farthest-ahead first, with time deltas.
///
/// Deltas use iRacing's CarIdxEstTime (time to reach the car's current
/// position on its lap). Because EstTime resets at the start/finish line,
/// cars on the other side of it are corrected by one estimated lap time.
/// </summary>
public static class RelativeCalculator
{
    /// <summary>Used when the roster has no usable class lap time yet.</summary>
    private const double FallbackLapTimeSeconds = 120;

    public static IReadOnlyList<RelativeRow> Compute(
        TelemetrySnapshot snapshot,
        SessionMetadata? metadata,
        int slotsPerSide = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(slotsPerSide, 1);

        if (!TryFindPlayer(snapshot, out var player))
        {
            return [];
        }

        var lapTime = ResolveLapTime(metadata, snapshot.PlayerCarIdx);
        var entries = new List<(CarTelemetry Car, double Delta)>();

        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == player.CarIdx || car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            // Cars absent from the roster are pace cars or spectators.
            if (metadata is not null && !metadata.DriversByCarIdx.ContainsKey(car.CarIdx))
            {
                continue;
            }

            entries.Add((car, ComputeDelta(car, player, lapTime)));
        }

        var ahead = entries
            .Where(e => e.Delta > 0)
            .OrderBy(e => e.Delta)
            .Take(slotsPerSide)
            .Reverse(); // farthest ahead shown first

        var behind = entries
            .Where(e => e.Delta <= 0)
            .OrderByDescending(e => e.Delta)
            .Take(slotsPerSide);

        var rows = new List<RelativeRow>(slotsPerSide * 2 + 1);
        rows.AddRange(ahead.Select(e => ToRow(e.Car, e.Delta, player, metadata)));
        rows.Add(ToRow(player, 0, player, metadata));
        rows.AddRange(behind.Select(e => ToRow(e.Car, e.Delta, player, metadata)));

        return rows;
    }

    private static bool TryFindPlayer(TelemetrySnapshot snapshot, out CarTelemetry player)
    {
        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == snapshot.PlayerCarIdx && car.Surface != CarTrackSurface.NotInWorld)
            {
                player = car;
                return true;
            }
        }

        player = default;
        return false;
    }

    private static double ResolveLapTime(SessionMetadata? metadata, int playerCarIdx)
    {
        if (metadata is not null
            && metadata.DriversByCarIdx.TryGetValue(playerCarIdx, out var driver)
            && driver.ClassEstLapTimeSeconds > 0)
        {
            return driver.ClassEstLapTimeSeconds;
        }

        return FallbackLapTimeSeconds;
    }

    private static double ComputeDelta(in CarTelemetry other, in CarTelemetry player, double lapTime)
    {
        double delta = other.EstTimeSeconds - player.EstTimeSeconds;
        var distDiff = other.LapDistPct - player.LapDistPct;

        if (distDiff > 0.5f)
        {
            delta -= lapTime;
        }
        else if (distDiff < -0.5f)
        {
            delta += lapTime;
        }

        return delta;
    }

    private static LapDifference Classify(in CarTelemetry other, in CarTelemetry player)
    {
        double progressDiff = other.Lap + (double)other.LapDistPct - player.Lap - player.LapDistPct;

        return progressDiff switch
        {
            > 0.5 => LapDifference.LapAhead,
            < -0.5 => LapDifference.LapBehind,
            _ => LapDifference.SameLap,
        };
    }

    private static RelativeRow ToRow(
        in CarTelemetry car,
        double deltaSeconds,
        in CarTelemetry player,
        SessionMetadata? metadata)
    {
        RosterDriver? driver = null;
        metadata?.DriversByCarIdx.TryGetValue(car.CarIdx, out driver);

        var license = driver?.License ?? string.Empty;
        var irating = driver?.IRating ?? 0;

        return new RelativeRow(
            CarIdx: car.CarIdx,
            IsPlayer: car.CarIdx == player.CarIdx,
            Position: car.Position,
            CarNumber: driver?.CarNumber ?? string.Empty,
            DisplayName: driver?.DisplayName ?? $"Car {car.CarIdx}",
            License: license,
            LicenseTier: RatingFormat.ParseLicenseTier(license),
            IRating: irating,
            ClassShortName: driver?.ClassShortName ?? string.Empty,
            ClassColorHex: RatingFormat.NormalizeHexColor(driver?.ClassColorRaw),
            DeltaSeconds: deltaSeconds,
            LapDifference: Classify(car, player),
            InPits: car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall);
    }
}
