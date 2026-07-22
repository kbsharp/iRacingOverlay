using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Map;

/// <summary>
/// Picks the field the track map draws. Deliberately thin: unlike the radar there
/// is no range to filter by and no geometry to solve - the whole point of the map
/// is that it shows <b>everyone</b>, so the driver can see where the field is
/// spread rather than only who is within a few car lengths.
/// </summary>
public static class TrackMapCalculator
{
    /// <summary>
    /// Every car worth drawing, ordered so the player is last - the widget draws in
    /// list order, so that puts your own mark on top of whoever you are running
    /// alongside. Pace cars and spectators (absent from the roster) are left out;
    /// cars in the pit lane are kept and flagged, since "my rival is in the lane"
    /// is exactly the thing the map is being glanced at for.
    /// </summary>
    public static IReadOnlyList<TrackMapCar> Compute(
        TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var cars = new List<TrackMapCar>(snapshot.Cars.Count);
        TrackMapCar? player = null;

        foreach (var car in snapshot.Cars)
        {
            if (car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            RosterDriver? driver = null;
            if (metadata is not null && !metadata.DriversByCarIdx.TryGetValue(car.CarIdx, out driver))
            {
                continue; // pace car or spectator
            }

            var mapped = new TrackMapCar(
                CarIdx: car.CarIdx,
                LapDistPct: car.LapDistPct,
                CarNumber: driver?.CarNumber ?? string.Empty,
                ClassColorHex: RatingFormat.NormalizeHexColor(driver?.ClassColorRaw),
                IsPlayer: car.CarIdx == snapshot.PlayerCarIdx,
                InPits: car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall);

            if (mapped.IsPlayer)
            {
                player = mapped;
            }
            else
            {
                cars.Add(mapped);
            }
        }

        if (player is { } own)
        {
            cars.Add(own);
        }

        return cars;
    }
}
