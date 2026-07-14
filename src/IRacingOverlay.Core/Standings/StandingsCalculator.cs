using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Standings;

/// <summary>
/// Builds the class-grouped standings: every eligible car ordered by position
/// within its class, with best/last lap times and gaps to the class leader.
///
/// Gaps come from iRacing's CarIdxF2Time (time behind the session leader);
/// subtracting the class leader's value gives a within-class gap that is
/// correct regardless of whether F2Time is measured against the overall or
/// class leader, since it's a difference either way.
/// </summary>
public static class StandingsCalculator
{
    /// <summary>iRacing reports position 0 for cars with no classified position yet.</summary>
    private const int Unclassified = int.MaxValue;

    public static IReadOnlyList<StandingsClassGroup> Compute(
        TelemetrySnapshot snapshot,
        SessionMetadata? metadata,
        int maxPerClass = 12)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPerClass, 1);

        var entries = new List<Entry>();
        foreach (var car in snapshot.Cars)
        {
            if (car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            RosterDriver? driver = null;
            metadata?.DriversByCarIdx.TryGetValue(car.CarIdx, out driver);

            // With a roster present, a missing entry means a pace car or spectator.
            if (metadata is not null && driver is null)
            {
                continue;
            }

            entries.Add(new Entry(car, driver));
        }

        if (entries.Count == 0)
        {
            return [];
        }

        // The single fastest valid best lap in the field gets the "session best"
        // flag (rendered in purple, the way iRacing's own timing does).
        var sessionBest = entries
            .Select(e => e.Car.BestLapTimeSeconds)
            .Where(b => b > 0)
            .DefaultIfEmpty(0f)
            .Min();

        return entries
            .GroupBy(e => e.Driver?.ClassShortName ?? string.Empty)
            .Select(g => BuildGroup(g.Key, [.. g], snapshot.PlayerCarIdx, maxPerClass, sessionBest))
            // Show the class containing the best overall position first.
            .OrderBy(g => g.LeaderOverallPosition)
            .ThenBy(g => g.Group.ClassShortName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Group)
            .ToList();
    }

    private static (StandingsClassGroup Group, int LeaderOverallPosition) BuildGroup(
        string className, List<Entry> classEntries, int playerCarIdx, int maxPerClass, float sessionBest)
    {
        classEntries.Sort(static (a, b) =>
        {
            var cmp = OrderKey(a.Car).CompareTo(OrderKey(b.Car));
            return cmp != 0 ? cmp : a.Car.CarIdx.CompareTo(b.Car.CarIdx);
        });

        var leader = classEntries[0].Car;
        var leaderF2 = leader.F2TimeSeconds;
        var classColor = classEntries
            .Select(e => e.Driver?.ClassColorRaw)
            .FirstOrDefault(c => c is not null);

        var rows = new List<StandingsRow>(classEntries.Count);
        for (var i = 0; i < classEntries.Count; i++)
        {
            rows.Add(ToRow(classEntries[i], i, leader, leaderF2, leader.BestLapTimeSeconds, className, playerCarIdx, sessionBest));
        }

        var shown = rows.Take(maxPerClass).ToList();

        // Always keep the player's own row visible, even outside the shown window.
        if (!shown.Any(r => r.IsPlayer))
        {
            var playerRow = rows.FirstOrDefault(r => r.IsPlayer);
            if (playerRow is not null)
            {
                shown.Add(playerRow);
            }
        }

        var group = new StandingsClassGroup(
            className,
            RatingFormat.NormalizeHexColor(classColor),
            shown);

        return (group, leader.Position > 0 ? leader.Position : Unclassified);
    }

    private static StandingsRow ToRow(
        in Entry entry, int indexInClass, in CarTelemetry leader, float leaderF2, float leaderBestLap,
        string className, int playerCarIdx, float sessionBest)
    {
        var car = entry.Car;
        var driver = entry.Driver;

        var isLeader = indexInClass == 0;

        double? gap;
        if (isLeader)
        {
            gap = 0;
        }
        else
        {
            var raw = car.F2TimeSeconds - leaderF2;
            gap = raw > 0 ? raw : null;
        }

        // Laps down comes from the time gap versus the class leader's lap time,
        // not a raw completed-lap difference - the latter flickers to "+1L" for
        // a car only tenths behind whenever the leader has just crossed the line.
        // Fall back to the lap-count difference only when no lap time is known.
        int lapsDown;
        if (isLeader || gap is null)
        {
            lapsDown = 0;
        }
        else if (leaderBestLap > 0)
        {
            // Whole laps of gap; naturally 0 for a sub-lap (same-lap) gap.
            lapsDown = (int)(gap.Value / leaderBestLap);
        }
        else
        {
            // No lap time to divide by - fall back to the raw completed-lap count.
            lapsDown = leader.LapsCompleted >= 0 && car.LapsCompleted >= 0
                ? Math.Max(0, leader.LapsCompleted - car.LapsCompleted)
                : 0;
        }

        var license = driver?.License ?? string.Empty;
        var irating = driver?.IRating ?? 0;
        var classPos = car.ClassPosition > 0 ? car.ClassPosition : indexInClass + 1;
        var best = car.BestLapTimeSeconds > 0 ? car.BestLapTimeSeconds : (double?)null;
        var isSessionBest = sessionBest > 0 && best is not null && best.Value <= sessionBest + 1e-4;

        return new StandingsRow(
            CarIdx: car.CarIdx,
            IsPlayer: car.CarIdx == playerCarIdx,
            OverallPosition: car.Position,
            ClassPosition: classPos,
            IsClassLeader: isLeader,
            CarNumber: driver?.CarNumber ?? string.Empty,
            DisplayName: driver?.DisplayName ?? $"Car {car.CarIdx}",
            License: license,
            LicenseTier: RatingFormat.ParseLicenseTier(license),
            IRating: irating,
            IRatingTier: RatingFormat.ClassifyIRating(irating),
            ClassShortName: className,
            ClassColorHex: RatingFormat.NormalizeHexColor(driver?.ClassColorRaw),
            BestLapSeconds: best,
            LastLapSeconds: car.LastLapTimeSeconds > 0 ? car.LastLapTimeSeconds : null,
            GapToClassLeaderSeconds: gap,
            LapsDown: lapsDown,
            IsSessionBestLap: isSessionBest,
            InPits: car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall);
    }

    private static int OrderKey(in CarTelemetry car) => car.Position > 0 ? car.Position : Unclassified;

    private readonly record struct Entry(CarTelemetry Car, RosterDriver? Driver);
}
