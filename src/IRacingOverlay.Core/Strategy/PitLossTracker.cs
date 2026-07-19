using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// Learns what a pit stop costs at this track, in seconds, by watching the
/// whole field stop.
///
/// The number cannot be computed from the sim: iRacing publishes a pit speed
/// limit but not a pit-lane length, and the loss also depends on where entry
/// and exit rejoin the racing line. So it is measured instead - and measured
/// off *other people's* stops, not just the player's, because a projection that
/// only works after your own first stop arrives long after the decision it was
/// meant to inform.
///
/// The measurement is a car's growth in <c>F2Time</c> (time behind the session
/// leader) across its visit to pit road. That is precisely the quantity wanted:
/// how far back the stop dropped them, in the same currency the projection then
/// spends. Elapsed wall-clock time in the lane would need the racing-pace time
/// for the same stretch subtracted back off; F2Time has already done that
/// subtraction, because it is measured against a car that stayed out.
///
/// What it deliberately does not do is separate the drive-through from the time
/// stood still. A projection built on "transit + your predicted service time"
/// would need a fuel fill rate the sim never publishes, so the service half
/// would be a constant nobody could check. Reporting the observed total keeps
/// every part of the figure measured - and <see cref="MedianLossSeconds"/> is
/// shown on the widget, so the driver can see the number the projection used
/// and tell when it is wrong.
/// </summary>
public sealed class PitLossTracker
{
    /// <summary>Stops faster than this are a drive-through, a pit-road cut, or a
    /// car that never really stopped - not a service stop worth learning from.</summary>
    private const double MinPlausibleLossSeconds = 5.0;

    /// <summary>Beyond this the car served a penalty, repaired damage, or sat in
    /// its box waiting out a session. Real service stops don't take two minutes,
    /// and one such sample would drag the median a long way.</summary>
    private const double MaxPlausibleLossSeconds = 120.0;

    /// <summary>How many stops before the figure is worth acting on. One stop can
    /// be anyone's bad day; the median of three has seen the lane behave.</summary>
    private const int MinSamplesToTrust = 3;

    /// <summary>Only the recent past matters - the lane gets slower in the wet and
    /// faster as a race dries out.</summary>
    private const int MaxSamples = 12;

    private readonly Dictionary<int, CarState> _states = [];
    private readonly List<double> _samples = [];
    private readonly List<double> _sorted = [];

    private int _sessionNum = -1;

    /// <summary>Stops observed and accepted so far.</summary>
    public int SampleCount => _samples.Count;

    /// <summary>True once enough stops have been seen to trust the figure.</summary>
    public bool IsLearned => _samples.Count >= MinSamplesToTrust;

    /// <summary>
    /// The typical cost of a stop here, in seconds, or null until
    /// <see cref="IsLearned"/>. Median rather than mean: a single stop ruined by
    /// a slow release or a queue behind another car should not move it.
    /// </summary>
    public double? MedianLossSeconds
    {
        get
        {
            if (!IsLearned)
            {
                return null;
            }

            _sorted.Clear();
            _sorted.AddRange(_samples);
            _sorted.Sort();

            var mid = _sorted.Count / 2;
            return _sorted.Count % 2 == 1
                ? _sorted[mid]
                : (_sorted[mid - 1] + _sorted[mid]) / 2.0;
        }
    }

    /// <summary>Forgets everything. Called automatically when the session changes -
    /// a practice lane and a race lane are not the same measurement.</summary>
    public void Reset()
    {
        _states.Clear();
        _samples.Clear();
    }

    /// <summary>
    /// Records this frame. Watches each car for the two pit-road edges: crossing
    /// in banks where the car stood relative to the leader, crossing back out
    /// banks the difference.
    ///
    /// A stop is only measured when both edges were actually seen. A car that was
    /// already in the lane when we started watching has no "before", and measuring
    /// from the first in-lane frame would report a fraction of the real loss - the
    /// stop would look cheap, which is the one direction the error must not go.
    /// </summary>
    public void Update(TelemetrySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SessionNum != _sessionNum)
        {
            _sessionNum = snapshot.SessionNum;
            Reset();
        }

        foreach (var car in snapshot.Cars)
        {
            // A disconnect mid-stop drops the car's history rather than banking a
            // half-measured stop when it reappears.
            if (car.Surface == CarTrackSurface.NotInWorld)
            {
                _states.Remove(car.CarIdx);
                continue;
            }

            var inLane = car.OnPitRoad || car.Surface == CarTrackSurface.InPitStall;
            var known = _states.TryGetValue(car.CarIdx, out var state);

            if (inLane)
            {
                // Entry is the last value seen *on track*: F2Time is already
                // climbing by the first in-lane frame.
                if (known && !state.InLane)
                {
                    _states[car.CarIdx] = state with { InLane = true, EntryF2 = state.LastOnTrackF2 };
                }

                continue;
            }

            if (known && state.InLane && IsUsable(state.EntryF2) && IsUsable(car.F2TimeSeconds))
            {
                Record(car.F2TimeSeconds - state.EntryF2);
            }

            _states[car.CarIdx] = new CarState(InLane: false, LastOnTrackF2: car.F2TimeSeconds, EntryF2: 0);
        }
    }

    /// <summary>
    /// iRacing reports a negative F2Time for a car with no classified gap yet.
    /// Zero is kept: that is the leader, who pits like everyone else. A lead
    /// change during their stop does corrupt that one sample - which is what the
    /// plausibility bounds and the median are there to absorb.
    /// </summary>
    private static bool IsUsable(double f2Seconds) => f2Seconds >= 0;

    /// <summary>
    /// Banks one stop, if it looks like a stop. The bounds are wide on purpose:
    /// they exist to throw out drive-throughs and damage repairs, not to nudge
    /// the figure toward what a pit stop "should" cost.
    /// </summary>
    private void Record(double lossSeconds)
    {
        if (lossSeconds is < MinPlausibleLossSeconds or > MaxPlausibleLossSeconds)
        {
            return;
        }

        _samples.Add(lossSeconds);

        if (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }
    }

    /// <summary>One car's progress through the lane. <see cref="LastOnTrackF2"/> is
    /// carried so that entry can be taken from before the lane started inflating it.</summary>
    private readonly record struct CarState(bool InLane, double LastOnTrackF2, double EntryF2);
}
