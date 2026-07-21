using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// The multiclass question a solo racer has no spotter for: faster traffic is
/// coming - where does it reach me, and when? Answers with the nearest
/// faster-class car behind, the sector it catches the player in, and the laps
/// until it does, so the pass can be planned rather than stumbled into.
///
/// The parts are all measured or published, none invented:
/// <list type="bullet">
/// <item>The <b>gap</b> is the same on-track delta the relative shows, off
/// iRacing's CarIdxEstTime (<see cref="RelativeCalculator.TrackGapSeconds"/>) -
/// a real separation, not a guess.</item>
/// <item>The <b>closing rate</b> is the difference in the two classes' estimated
/// lap times - the sim's own per-class pace. A slower or same-class car is not a
/// threat and never appears.</item>
/// <item>The <b>meeting point</b> is where the player will be after that many
/// laps, named by its timing sector (<see cref="TrackSectors"/>).</item>
/// </list>
///
/// Deliberately only the car currently behind and within a few laps: a warning
/// you can't act on yet is noise, and this is the one that changes the next
/// corner. The forecast assumes today's pace holds - a car stuck in its own
/// battle isn't really closing at class pace - so it is advisory and, like every
/// other readout here, checkable a lap later.
/// </summary>
public static class TrafficForecaster
{
    /// <summary>Beyond this the car is too far back to plan around yet; the
    /// forecast stays silent until it is worth acting on.</summary>
    private const double MaxLapsToContact = 3.0;

    /// <summary>A class only counts as "faster" once it takes back at least this
    /// much per lap; inside it the two are matched and there is nothing to yield.</summary>
    private const double MinClosingSecondsPerLap = 0.3;

    /// <summary>
    /// The most imminent incoming faster-class car, or
    /// <see cref="TrafficThreat.None"/> when there isn't one to act on.
    /// </summary>
    public static TrafficThreat Compute(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (metadata is null || !TryFindPlayer(snapshot, out var player))
        {
            return TrafficThreat.None;
        }

        // A player in the lane isn't racing traffic - the gaps are all pit-speed.
        if (player.OnPitRoad || player.Surface == CarTrackSurface.InPitStall)
        {
            return TrafficThreat.None;
        }

        if (!metadata.DriversByCarIdx.TryGetValue(player.CarIdx, out var playerDriver)
            || playerDriver.ClassEstLapTimeSeconds <= 0)
        {
            return TrafficThreat.None;
        }

        var playerPace = playerDriver.ClassEstLapTimeSeconds;
        var playerClass = playerDriver.ClassShortName;
        var lapTime = RelativeCalculator.ResolveLapTimeSeconds(metadata, player.CarIdx);

        var best = TrafficThreat.None;
        var bestLaps = double.MaxValue;

        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == player.CarIdx || car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            // A car in its box isn't closing on anyone.
            if (car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall)
            {
                continue;
            }

            if (!metadata.DriversByCarIdx.TryGetValue(car.CarIdx, out var driver))
            {
                continue; // pace car or spectator
            }

            // Same-class battles are the relative's pace-trend job, not traffic.
            if (driver.ClassShortName == playerClass || driver.ClassEstLapTimeSeconds <= 0)
            {
                continue;
            }

            var closing = playerPace - driver.ClassEstLapTimeSeconds;
            if (closing < MinClosingSecondsPerLap)
            {
                continue; // not a faster class
            }

            // Only cars behind on track are catching from behind; a negative gap
            // is the relative's "behind" sign.
            var gap = -RelativeCalculator.TrackGapSeconds(car, player, lapTime);
            if (gap <= 0)
            {
                continue;
            }

            var laps = gap / closing;
            if (laps > MaxLapsToContact || laps >= bestLaps)
            {
                continue;
            }

            bestLaps = laps;
            var meetingPct = player.LapDistPct + laps;
            best = new TrafficThreat(
                CarNumber: driver.CarNumber,
                ClassShortName: driver.ClassShortName,
                ClassColorRaw: driver.ClassColorRaw,
                GapSeconds: gap,
                ClosingRateSecondsPerLap: closing,
                LapsToContact: laps,
                MeetingSector: TrackSectors.SectorAt(meetingPct, metadata.SectorStartPcts));
        }

        return best;
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
}
