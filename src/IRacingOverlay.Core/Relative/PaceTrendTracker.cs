using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Relative;

/// <summary>
/// Turns the relative's per-frame deltas into a forecast: is this battle
/// actually arriving, and when?
///
/// A single delta says where someone is; the trend says where they will be.
/// That is the decision - push now to make the pass stick before the flag, or
/// stop defending someone who is never getting there.
///
/// How it behaves, and why:
///
/// <list type="bullet">
/// <item>The rate is a <b>least-squares slope</b> over a rolling window, not a
/// difference between two frames. Deltas jitter by tenths corner to corner
/// (traffic, a lift, EstTime granularity); a two-point rate would read
/// "closing 4s/lap" one frame and "pulling away" the next.</item>
/// <item>It reports in <b>seconds per lap</b>, not per second, because that is
/// the unit a driver already thinks in.</item>
/// <item>History is <b>discarded on discontinuity</b> - a pit stop, a tow, a
/// reset, or any single-frame jump too large to be pace. Regressing across one
/// of those invents a closing rate that never happened.</item>
/// <item>Nothing is reported until the window holds a real span of time. An
/// early guess from three frames is worse than no number at all.</item>
/// </list>
///
/// Feed it the rows from <see cref="RelativeCalculator"/> each frame, then read
/// <see cref="For"/> per car.
/// </summary>
public sealed class PaceTrendTracker
{
    /// <summary>How far back the regression looks. Long enough to average out corner-by-corner
    /// jitter, short enough to notice when someone starts pushing.</summary>
    private const double WindowSeconds = 45;

    /// <summary>Minimum spread of samples before a rate is trustworthy.</summary>
    private const double MinSpanSeconds = 12;

    private const int MinSamples = 6;

    /// <summary>Below this the gap is holding: it is inside the noise a driver can't act on.</summary>
    private const double HoldingThresholdPerLap = 0.05;

    /// <summary>A single-frame gap change larger than this is not pace - it's a pit stop,
    /// a tow, or the field being re-ordered around us. Start the history again.</summary>
    private const double DiscontinuitySeconds = 3.0;

    /// <summary>Beyond this, "in N laps" is arithmetic rather than a forecast.</summary>
    private const double MaxLapsToContact = 40;

    private readonly Dictionary<int, History> _histories = [];
    private readonly List<int> _stale = [];

    private int _sessionNum = -1;

    /// <summary>Drops every car's history. Called automatically when the session changes.</summary>
    public void Reset()
    {
        _histories.Clear();
    }

    /// <summary>
    /// Records this frame's gaps and refreshes every forecast. Cheap and
    /// allocation-free once the field has settled.
    /// </summary>
    public void Update(
        TelemetrySnapshot snapshot,
        SessionMetadata? metadata,
        IReadOnlyList<RelativeRow> rows)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rows);

        if (snapshot.SessionNum != _sessionNum)
        {
            _sessionNum = snapshot.SessionNum;
            Reset();
        }

        var lapTime = RelativeCalculator.ResolveLapTimeSeconds(metadata, snapshot.PlayerCarIdx);
        var lapsRemaining = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            snapshot.SessionLapsRemain, snapshot.SessionTimeRemainSeconds, lapTime);

        var playerInPits = false;
        foreach (var row in rows)
        {
            if (row.IsPlayer)
            {
                playerInPits = row.InPits;
                break;
            }
        }

        foreach (var row in rows)
        {
            if (row.IsPlayer)
            {
                continue;
            }

            // A car in its box, or a player in theirs, isn't racing anyone: the gap
            // is running away at pit-lane speed and says nothing about pace.
            if (row.InPits || playerInPits)
            {
                _histories.Remove(row.CarIdx);
                continue;
            }

            if (!_histories.TryGetValue(row.CarIdx, out var history))
            {
                _histories[row.CarIdx] = history = new History();
            }

            history.Add(snapshot.SessionTimeSeconds, Math.Abs(row.DeltaSeconds));
            history.Trend = history.Project(lapTime, lapsRemaining);
        }

        Prune(snapshot.SessionTimeSeconds, rows);
    }

    /// <summary>The current forecast for a car, or <see cref="PaceTrend.None"/> if there isn't one.</summary>
    public PaceTrend For(int carIdx) =>
        _histories.TryGetValue(carIdx, out var history) ? history.Trend : PaceTrend.None;

    /// <summary>
    /// Forgets cars that have dropped out of the relative. Without this, a car
    /// lapped out of view keeps its stale history and reappears - laps later -
    /// with a rate regressed across the intervening gap.
    /// </summary>
    private void Prune(double now, IReadOnlyList<RelativeRow> rows)
    {
        _stale.Clear();

        foreach (var (carIdx, history) in _histories)
        {
            var present = false;
            foreach (var row in rows)
            {
                if (row.CarIdx == carIdx)
                {
                    present = true;
                    break;
                }
            }

            if (!present && now - history.LastSampleTime > WindowSeconds)
            {
                _stale.Add(carIdx);
            }
        }

        foreach (var carIdx in _stale)
        {
            _histories.Remove(carIdx);
        }
    }

    /// <summary>One car's rolling window of gap samples, plus its latest forecast.</summary>
    private sealed class History
    {
        private readonly List<(double Time, double Gap)> _samples = [];

        public PaceTrend Trend { get; set; } = PaceTrend.None;

        public double LastSampleTime { get; private set; }

        public void Add(double time, double gap)
        {
            if (_samples.Count > 0)
            {
                var last = _samples[^1];

                // The same frame re-rendered (a settings change replays the last
                // snapshot) is not new information - and recording it twice would
                // weight that instant double in the regression.
                if (time == last.Time)
                {
                    return;
                }

                // Time running backwards means a session restart or a replay scrub.
                if (time < last.Time || Math.Abs(gap - last.Gap) > DiscontinuitySeconds)
                {
                    _samples.Clear();
                    Trend = PaceTrend.None;
                }
            }

            _samples.Add((time, gap));
            LastSampleTime = time;

            var cutoff = time - WindowSeconds;
            var drop = 0;
            while (drop < _samples.Count && _samples[drop].Time < cutoff)
            {
                drop++;
            }

            if (drop > 0)
            {
                _samples.RemoveRange(0, drop);
            }
        }

        /// <summary>
        /// Least-squares slope of gap against time, expressed per lap and signed
        /// so that positive means the gap is shrinking.
        /// </summary>
        public PaceTrend Project(double lapTimeSeconds, double? lapsRemaining)
        {
            if (_samples.Count < MinSamples || lapTimeSeconds <= 0)
            {
                return PaceTrend.None;
            }

            var span = _samples[^1].Time - _samples[0].Time;
            if (span < MinSpanSeconds)
            {
                return PaceTrend.None;
            }

            double sumT = 0, sumG = 0;
            foreach (var (time, gap) in _samples)
            {
                sumT += time;
                sumG += gap;
            }

            var meanT = sumT / _samples.Count;
            var meanG = sumG / _samples.Count;

            double covariance = 0, variance = 0;
            foreach (var (time, gap) in _samples)
            {
                var dt = time - meanT;
                covariance += dt * (gap - meanG);
                variance += dt * dt;
            }

            if (variance <= 0)
            {
                return PaceTrend.None;
            }

            // Slope is gap-seconds per elapsed second; a lap's worth is what we show.
            var closingPerLap = -(covariance / variance) * lapTimeSeconds;

            if (Math.Abs(closingPerLap) < HoldingThresholdPerLap)
            {
                return new PaceTrend(PaceTrendDirection.Holding, 0, null, null);
            }

            if (closingPerLap < 0)
            {
                return new PaceTrend(PaceTrendDirection.Pulling, closingPerLap, null, null);
            }

            var laps = _samples[^1].Gap / closingPerLap;
            double? lapsToContact = laps <= MaxLapsToContact ? laps : null;

            bool? arrives = lapsRemaining is { } remaining && lapsToContact is { } eta
                ? eta <= remaining
                : null;

            return new PaceTrend(PaceTrendDirection.Closing, closingPerLap, lapsToContact, arrives);
        }
    }
}
