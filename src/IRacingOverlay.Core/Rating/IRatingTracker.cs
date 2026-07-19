using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Rating;

/// <summary>
/// Turns a stream of telemetry frames into a stable projected iRating change.
///
/// The maths lives in <see cref="IRatingCalculator"/>; what lives here is
/// everything that stops a live projection from misbehaving in a real race:
///
/// <list type="bullet">
/// <item>Only race sessions count — practice and qualifying never move iRating.</item>
/// <item>Only the player's own class counts; iRacing rates each class separately.</item>
/// <item>Nothing shows until the player has completed a racing lap. Grid order is
/// inherited from qualifying, so a projection before then is reporting on a race
/// that hasn't happened.</item>
/// <item>The field is <b>sticky</b>. A driver who disconnects stays in the field at
/// their rating and is classified behind everyone still running, ordered by laps
/// completed — which is what iRacing does with a DNF. Without this the field
/// shrinks under you as people rage-quit and the projection swings by tens of
/// points for reasons that have nothing to do with your driving.</item>
/// <item>At the flag the value is <b>captured</b>. Once the player crosses the line
/// under the checkered, the projection freezes: the post-race grid empties out
/// within seconds, and a live number would drift long after the result is settled.</item>
/// </list>
///
/// Feed every frame to <see cref="Update"/>; it is cheap and allocation-light
/// between field-size changes.
/// </summary>
public sealed class IRatingTracker
{
    /// <summary>iRacing's session-type name for a race, as normalised by <see cref="SessionFormat.ResolveSessionType"/>.</summary>
    private const string RaceSessionType = "RACE";

    private readonly Dictionary<int, Competitor> _field = [];
    private readonly List<Competitor> _order = [];
    private readonly List<int> _ratings = [];

    private int _sessionNum = -1;
    private int _checkeredAtLap = -1;
    private IRatingProjection _current = IRatingProjection.None;

    /// <summary>The most recent projection. Also the return value of <see cref="Update"/>.</summary>
    public IRatingProjection Current => _current;

    /// <summary>Drops all per-race state. Called automatically when the session number changes.</summary>
    public void Reset()
    {
        _field.Clear();
        _checkeredAtLap = -1;
        _current = IRatingProjection.None;
    }

    public IRatingProjection Update(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SessionNum != _sessionNum)
        {
            _sessionNum = snapshot.SessionNum;
            Reset();
        }

        // Once captured, the result stands - the field is draining and nothing
        // it does now can change what the player scored.
        if (_current.State == IRatingProjectionState.Final)
        {
            return _current;
        }

        if (metadata is null
            || SessionFormat.ResolveSessionType(metadata.SessionTypesByNum, snapshot.SessionNum) != RaceSessionType
            || !metadata.DriversByCarIdx.TryGetValue(snapshot.PlayerCarIdx, out var player)
            || player.IRating <= 0)
        {
            return _current = IRatingProjection.None;
        }

        var playerCar = FindPlayerCar(snapshot);
        Absorb(snapshot, metadata, player.ClassShortName);

        if (playerCar is not { } car || !_field.ContainsKey(snapshot.PlayerCarIdx))
        {
            return _current;
        }

        // Grid position is a qualifying result, not a race result. Wait for a lap.
        if (car.LapsCompleted < 1)
        {
            return _current = _current with { State = IRatingProjectionState.Pending };
        }

        var projection = Project(player.IRating, snapshot.PlayerCarIdx);

        if (projection is null)
        {
            return _current = IRatingProjection.None;
        }

        return _current = projection with { State = FinishState(snapshot, car) };
    }

    /// <summary>
    /// Live until the player takes the flag. iRacing raises the checkered when the
    /// leader finishes, so the player is done one line-crossing later; a player who
    /// leaves the world after the checkered (retired, or towed to the paddock) is
    /// done too.
    /// </summary>
    private IRatingProjectionState FinishState(TelemetrySnapshot snapshot, in CarTelemetry car)
    {
        var checkered = SessionFlagResolver.Resolve(snapshot.Flags) == SessionFlagState.Checkered
            || snapshot.Flags.HasFlag(SessionFlags.Checkered);

        if (!checkered)
        {
            return IRatingProjectionState.Live;
        }

        if (_checkeredAtLap < 0)
        {
            _checkeredAtLap = car.LapsCompleted;
        }

        return car.LapsCompleted > _checkeredAtLap || car.Surface == CarTrackSurface.NotInWorld
            ? IRatingProjectionState.Final
            : IRatingProjectionState.Live;
    }

    /// <summary>Records everyone currently on track, and marks the absent as retired.</summary>
    private void Absorb(TelemetrySnapshot snapshot, SessionMetadata metadata, string playerClass)
    {
        foreach (var (_, competitor) in _field)
        {
            competitor.InWorld = false;
        }

        foreach (var car in snapshot.Cars)
        {
            if (car.Surface == CarTrackSurface.NotInWorld
                || !metadata.DriversByCarIdx.TryGetValue(car.CarIdx, out var driver)
                || driver.IRating <= 0
                || !string.Equals(driver.ClassShortName, playerClass, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!_field.TryGetValue(car.CarIdx, out var competitor))
            {
                _field[car.CarIdx] = competitor = new Competitor(car.CarIdx, driver.IRating);
            }

            competitor.InWorld = true;
            competitor.LapsCompleted = Math.Max(competitor.LapsCompleted, car.LapsCompleted);
            competitor.ClassPosition = car.ClassPosition > 0 ? car.ClassPosition : competitor.ClassPosition;
        }
    }

    /// <summary>
    /// Orders the sticky field - runners by class position, retirements behind
    /// them by laps completed - and prices the player's slot in it.
    /// </summary>
    private IRatingProjection? Project(int currentRating, int playerCarIdx)
    {
        _order.Clear();
        _order.AddRange(_field.Values);
        _order.Sort(CompareForClassification);

        _ratings.Clear();
        var playerIndex = -1;

        for (var i = 0; i < _order.Count; i++)
        {
            _ratings.Add(_order[i].IRating);

            if (_order[i].CarIdx == playerCarIdx)
            {
                playerIndex = i;
            }
        }

        if (playerIndex < 0 || _ratings.Count < 2)
        {
            return null;
        }

        var position = playerIndex + 1;
        var delta = IRatingCalculator.Delta(_ratings, playerIndex, position);

        return new IRatingProjection(
            IRatingProjectionState.Live,
            delta,
            currentRating + delta,
            currentRating,
            position,
            _ratings.Count);
    }

    private static int CompareForClassification(Competitor a, Competitor b)
    {
        // Anyone still circulating is classified ahead of anyone who isn't.
        if (a.InWorld != b.InWorld)
        {
            return a.InWorld ? -1 : 1;
        }

        if (a.InWorld)
        {
            var byPosition = Rank(a).CompareTo(Rank(b));
            return byPosition != 0 ? byPosition : a.CarIdx.CompareTo(b.CarIdx);
        }

        // Retirements are separated by how far they got, as iRacing classifies a DNF.
        var byLaps = b.LapsCompleted.CompareTo(a.LapsCompleted);
        return byLaps != 0 ? byLaps : a.CarIdx.CompareTo(b.CarIdx);
    }

    /// <summary>Cars with no classified position yet sort to the back of the runners.</summary>
    private static int Rank(Competitor competitor) =>
        competitor.ClassPosition > 0 ? competitor.ClassPosition : int.MaxValue;

    private static CarTelemetry? FindPlayerCar(TelemetrySnapshot snapshot)
    {
        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == snapshot.PlayerCarIdx)
            {
                return car;
            }
        }

        return null;
    }

    private sealed class Competitor(int carIdx, int iRating)
    {
        public int CarIdx { get; } = carIdx;

        public int IRating { get; } = iRating;

        public bool InWorld { get; set; }

        public int LapsCompleted { get; set; }

        public int ClassPosition { get; set; }
    }
}
