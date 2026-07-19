using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Standings;

/// <summary>
/// Remembers where every car started the race, so the standings can show how
/// many places each has gained or lost since.
///
/// iRacing exposes no "starting position" variable, so it has to be latched from
/// telemetry: each car's class position is recorded the <b>first</b> time this
/// tracker sees it in a race session, and never revised. Before the green,
/// positions are grid order, so an overlay that was already running when the race
/// began records the grid - which is the normal case for a tray app left running
/// across a session.
///
/// Deliberate behaviours:
///
/// <list type="bullet">
/// <item><b>Race sessions only.</b> Practice and qualifying order cars by lap
/// time, so "positions gained" there would count improved laps, not overtakes.</item>
/// <item><b>Cars are latched individually.</b> A car whose position reads 0 on the
/// first frame (iRacing reports 0 before it classifies a car) is simply recorded a
/// frame or two later, still on the grid. A car that joins late is baselined where
/// it joined rather than being left without an arrow forever.</item>
/// <item><b>A session-number change resets everything</b>, so the qualifying order
/// never leaks into the race's baseline.</item>
/// </list>
///
/// The one case this cannot get right is an overlay launched <i>mid-race</i>: the
/// baseline is then wherever the field was when the overlay attached, not the
/// grid. There is no telemetry signal that separates that from a genuine start,
/// because as far as the sim is concerned they are the same frame.
/// </summary>
public sealed class StartPositionTracker
{
    /// <summary>iRacing's session-type name for a race, as normalised by <see cref="SessionFormat.ResolveSessionType"/>.</summary>
    private const string RaceSessionType = "RACE";

    private static readonly Dictionary<int, int> Empty = [];

    private readonly Dictionary<int, int> _startPositions = [];

    private int _sessionNum = -1;

    /// <summary>Starting class position by car index; empty outside a race.</summary>
    public IReadOnlyDictionary<int, int> StartPositions => _startPositions;

    /// <summary>Drops every baseline. Called automatically when the session number changes.</summary>
    public void Reset() => _startPositions.Clear();

    public IReadOnlyDictionary<int, int> Update(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SessionNum != _sessionNum)
        {
            _sessionNum = snapshot.SessionNum;
            Reset();
        }

        if (SessionFormat.ResolveSessionType(metadata?.SessionTypesByNum, snapshot.SessionNum) != RaceSessionType)
        {
            Reset();
            return Empty;
        }

        foreach (var car in snapshot.Cars)
        {
            if (car.Surface == CarTrackSurface.NotInWorld || _startPositions.ContainsKey(car.CarIdx))
            {
                continue;
            }

            // Class position is what the standings' P column shows, so the arrow
            // beside it has to count the same thing. Fall back to the overall
            // position for builds/sessions that leave CarIdxClassPosition at 0.
            var position = car.ClassPosition > 0 ? car.ClassPosition : car.Position;

            if (position > 0)
            {
                _startPositions[car.CarIdx] = position;
            }
        }

        return _startPositions;
    }
}
