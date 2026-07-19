using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// Answers the question the fuel widget could never answer: not "can you make
/// it to the finish", but "what does stopping actually cost you".
///
/// The arithmetic is deliberately plain. Every car's <c>F2Time</c> is its time
/// behind the session leader, so the difference between two cars' F2Time is the
/// gap between them - which is how the standings already derives every gap and
/// interval it shows. Losing <c>pitLoss</c> seconds in the lane moves the player
/// back by exactly that much in the same currency, so the projection is: add the
/// pit loss to the player's F2Time, then read off who they land between.
///
/// Working in F2Time rather than lap counts is what makes lapped traffic behave.
/// A car a lap down already carries a whole lap time in its F2Time, so it sorts
/// behind the player without laps ever being counted separately - the same
/// time-based reasoning the standings uses to keep laps-down from flickering.
/// </summary>
public static class PitExitProjector
{
    /// <summary>
    /// Projects the rejoin. Returns <see cref="PitExitProjection.None"/> when the
    /// question doesn't apply or can't be answered honestly.
    /// </summary>
    /// <param name="pitLossSeconds">The learned cost of a stop - see
    /// <see cref="PitLossTracker"/>. Null until enough stops have been seen, which
    /// suppresses the projection rather than guessing at it.</param>
    public static PitExitProjection Compute(
        TelemetrySnapshot snapshot,
        SessionMetadata? metadata,
        double? pitLossSeconds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // F2Time only means "behind the leader" in a race; other session types
        // report a best lap time in the same field, which would project nonsense.
        if (!IsRace(snapshot, metadata) || pitLossSeconds is not > 0)
        {
            return PitExitProjection.None;
        }

        if (!TryFindPlayer(snapshot, out var player) || player.F2TimeSeconds <= 0)
        {
            return PitExitProjection.None;
        }

        var playerClass = ClassOf(metadata, player.CarIdx);
        var exitF2 = player.F2TimeSeconds + pitLossSeconds.Value;

        var overallAhead = 0;
        var classAhead = 0;
        var currentClassAhead = 0;

        // The nearest car on each side of where the player would land.
        double? gapAhead = null, gapBehind = null;
        var carAhead = string.Empty;
        var carBehind = string.Empty;

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

            if (car.F2TimeSeconds <= 0)
            {
                continue;
            }

            var sameClass = ClassOf(metadata, car.CarIdx) == playerClass;

            if (car.F2TimeSeconds < exitF2)
            {
                overallAhead++;

                if (sameClass)
                {
                    classAhead++;

                    var gap = exitF2 - car.F2TimeSeconds;
                    if (gapAhead is null || gap < gapAhead)
                    {
                        gapAhead = gap;
                        carAhead = NumberOf(metadata, car.CarIdx);
                    }
                }
            }
            else if (sameClass)
            {
                var gap = car.F2TimeSeconds - exitF2;
                if (gapBehind is null || gap < gapBehind)
                {
                    gapBehind = gap;
                    carBehind = NumberOf(metadata, car.CarIdx);
                }
            }

            // Where they stand *now*, so the projection can report what the stop costs
            // rather than only where it lands.
            if (sameClass && car.F2TimeSeconds < player.F2TimeSeconds)
            {
                currentClassAhead++;
            }
        }

        return new PitExitProjection(
            ClassPosition: classAhead + 1,
            OverallPosition: overallAhead + 1,
            PositionsLost: classAhead - currentClassAhead,
            PitLossSeconds: pitLossSeconds.Value,
            CarAheadNumber: carAhead,
            GapToCarAheadSeconds: gapAhead,
            CarBehindNumber: carBehind,
            GapToCarBehindSeconds: gapBehind);
    }

    private static bool IsRace(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        var type = SessionFormat.ResolveSessionType(metadata?.SessionTypesByNum, snapshot.SessionNum);
        return SessionFormat.ShortType(type) == "RACE";
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

    private static string ClassOf(SessionMetadata? metadata, int carIdx) =>
        metadata is not null && metadata.DriversByCarIdx.TryGetValue(carIdx, out var driver)
            ? driver.ClassShortName
            : string.Empty;

    private static string NumberOf(SessionMetadata? metadata, int carIdx) =>
        metadata is not null && metadata.DriversByCarIdx.TryGetValue(carIdx, out var driver)
            ? driver.CarNumber
            : string.Empty;
}
