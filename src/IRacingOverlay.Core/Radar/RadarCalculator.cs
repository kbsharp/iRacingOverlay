using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Radar;

/// <summary>
/// Builds the positional radar: the cars physically nearest the player, placed
/// in the player's local frame using the learned <see cref="TrackMap"/>. Pace
/// cars and spectators (absent from the roster) and cars in the pits are left
/// out - the radar is about wheel-to-wheel proximity on track.
/// </summary>
public static class RadarCalculator
{
    /// <summary>How far ahead/behind (along the track) a car is still drawn, metres.</summary>
    public const double DefaultRangeMeters = 60.0;

    /// <summary>
    /// Where the spotter puts a car it has named a side for: the centre of the
    /// neighbouring lane. Coarse on purpose - the sim only tells us the side, so
    /// the blip claims a side and nothing finer.
    /// </summary>
    public const double SpotterLaneOffsetMeters = RadarDanger.NeighbouringLaneMeters;

    public static RadarResult Compute(
        TelemetrySnapshot snapshot,
        SessionMetadata? metadata,
        TrackMap map,
        double trackLengthMeters,
        double rangeMeters = DefaultRangeMeters)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(map);

        if (trackLengthMeters <= 0 || !map.IsReady || !TryFindPlayer(snapshot, out var player))
        {
            return new RadarResult([], map.IsReady);
        }

        var blips = new List<RadarBlip>();
        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == player.CarIdx || car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            if (car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall)
            {
                continue;
            }

            // Cars absent from the roster are pace cars or spectators.
            if (metadata is not null && !metadata.DriversByCarIdx.ContainsKey(car.CarIdx))
            {
                continue;
            }

            // Cheap along-track reject before the heading walk.
            var arcMeters = Math.Abs(RadarGeometry.WrapSignedPct(car.LapDistPct - player.LapDistPct))
                * trackLengthMeters;
            if (arcMeters > rangeMeters)
            {
                continue;
            }

            var (right, forward, angle) = RadarGeometry.RelativeTo(
                map, player.LapDistPct, car.LapDistPct, trackLengthMeters);

            // The walk can bend a car just outside the box on a tight corner;
            // keep the honest longitudinal range as the gate.
            if (Math.Abs(forward) > rangeMeters)
            {
                continue;
            }

            RosterDriver? driver = null;
            metadata?.DriversByCarIdx.TryGetValue(car.CarIdx, out driver);

            blips.Add(new RadarBlip(
                CarIdx: car.CarIdx,
                RightMeters: right,
                ForwardMeters: forward,
                RelativeAngleRad: angle,
                CarNumber: driver?.CarNumber ?? string.Empty,
                ClassColorHex: RatingFormat.NormalizeHexColor(driver?.ClassColorRaw)));
        }

        var spotterLeft = RadarFormat.HasCarLeft(snapshot.CarLeftRight);
        var spotterRight = RadarFormat.HasCarRight(snapshot.CarLeftRight);
        ResolveStackedBlips(blips, snapshot.CarLeftRight, spotterLeft, spotterRight);

        var danger = RadarDanger.Compute(blips, spotterLeft, spotterRight);
        return new RadarResult(blips, MapReady: true, LeftDanger: danger.Left, RightDanger: danger.Right);
    }

    /// <summary>
    /// Second-guess the cars the walk has stacked on top of us. The geometry places
    /// a car from its along-track offset alone, so anyone level with us lands on the
    /// centreline - which looks exactly like an empty mirror, and on a straight that
    /// is every side-by-side car there is.
    ///
    /// iRacing's spotter is the only real lateral information the sim gives, so we
    /// defer to it, the same way the first-lap fallback does. It names a side, not a
    /// car, so we only move a blip when the attribution is unambiguous: one stacked
    /// car and one side reported. Otherwise the blip stays where it is and is marked
    /// unresolved, for the widget to draw as the doubt it is. "Clear" is an answer
    /// too - nobody is alongside, so a stacked car really is queued in our lane.
    /// </summary>
    private static void ResolveStackedBlips(
        List<RadarBlip> blips, CarLeftRight spotter, bool spotterLeft, bool spotterRight)
    {
        if (spotter is CarLeftRight.Off or CarLeftRight.Clear)
        {
            return;
        }

        var stacked = new List<int>();
        for (var i = 0; i < blips.Count; i++)
        {
            if (IsStacked(blips[i]))
            {
                stacked.Add(i);
            }
        }

        if (stacked.Count == 0)
        {
            return;
        }

        if (stacked.Count == 1 && spotterLeft != spotterRight)
        {
            var offset = spotterLeft ? -SpotterLaneOffsetMeters : SpotterLaneOffsetMeters;
            blips[stacked[0]] = blips[stacked[0]] with { RightMeters = offset };
            return;
        }

        foreach (var i in stacked)
        {
            blips[i] = blips[i] with { LateralUnresolved = true };
        }
    }

    /// <summary>
    /// True when the walk has drawn a car close enough to be alongside us but with
    /// less than a car's width of separation - a position no two cars can occupy, so
    /// it is the geometry saying "I don't know", not a measurement.
    /// </summary>
    private static bool IsStacked(RadarBlip blip)
        => Math.Abs(blip.ForwardMeters) < RadarDanger.OverlapRangeMeters
            && Math.Abs(blip.RightMeters) < RadarDanger.MinLateralMeters;

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
