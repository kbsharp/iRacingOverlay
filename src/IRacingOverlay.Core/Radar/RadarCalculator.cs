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

        return new RadarResult(blips, MapReady: true);
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
