using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Weather;

/// <summary>
/// Turns a stream of live weather frames into a <see cref="WeatherNowcast"/> - the
/// transition the track is <i>already going through</i>, in units the driver owns
/// (wetness steps and °C over the last few minutes).
///
/// <para>Why a nowcast and not a forecast: iRacing publishes no future weather to
/// third parties by any route (SDK shared memory, the authenticated /data web
/// API, or otherwise - researched Jul 2026; see the roadmap). The in-sim "View
/// Forecast" panel reads generated data the sim keeps to itself. So rather than
/// invent a forecast, this reports only what it has measured: how wet the track
/// is now versus how wet it was <see cref="LookbackSeconds"/> ago.</para>
///
/// <para>The comparison is against the oldest sample still inside the window,
/// which is what makes it self-heal: once a change is older than the window, the
/// reference has slid past it and the trend reads Steady again - so a squall that
/// blew through and settled stops flashing "wetting" a few minutes later, on its
/// own.</para>
///
/// Pure and deterministic - no clock of its own; it advances on the session time
/// it's handed. Not thread-safe; drive it from one thread (the UI thread, as the
/// other calculators are).
/// </summary>
public sealed class WeatherNowcaster
{
    /// <summary>How far back the trend reaches. Five minutes is long enough that a
    /// gradual drizzle registers, short enough that the reading tracks a changing
    /// sky rather than the whole session - and it keeps the alert up for about
    /// that long after rain arrives, which is exactly the window a tyre call is
    /// live in.</summary>
    public const double LookbackSeconds = 300.0;

    /// <summary>Below this much history the trend is noise, so the strip stays
    /// hidden until the window has filled enough to mean something.</summary>
    private const double MinObservationSeconds = 60.0;

    /// <summary>Track temp has to move at least this much over the window before
    /// it's called rising/falling rather than steady - below it, it's just the
    /// surface breathing.</summary>
    private const double TempTrendDeadbandC = 1.5;

    private readonly List<Sample> _history = [];

    private int _sessionNum = int.MinValue;
    private double _lastTime = double.NegativeInfinity;

    private readonly record struct Sample(double Time, TrackWetness Wetness, float TrackTempC);

    /// <summary>
    /// Feeds one telemetry frame and returns the current nowcast. Call every
    /// frame; the reading only changes as fast as the weather does.
    /// </summary>
    public WeatherNowcast Update(
        int sessionNum,
        double sessionTimeSeconds,
        TrackWetness wetness,
        float trackTempC,
        float airTempC)
    {
        // A new session, or a clock that jumped backwards (a session restart),
        // means the old samples describe a different sky - start over.
        if (sessionNum != _sessionNum || sessionTimeSeconds < _lastTime)
        {
            _history.Clear();
            _sessionNum = sessionNum;
        }

        _lastTime = sessionTimeSeconds;
        _history.Add(new Sample(sessionTimeSeconds, wetness, trackTempC));

        // Drop everything older than the window, but always keep at least the
        // current sample so there's a reference to read.
        var cutoff = sessionTimeSeconds - LookbackSeconds;
        var drop = 0;
        while (drop < _history.Count - 1 && _history[drop].Time < cutoff)
        {
            drop++;
        }

        if (drop > 0)
        {
            _history.RemoveRange(0, drop);
        }

        var reference = _history[0];
        var span = sessionTimeSeconds - reference.Time;

        var tempDelta = trackTempC - reference.TrackTempC;
        var tempTrend = tempDelta >= TempTrendDeadbandC ? TempTrend.Rising
            : tempDelta <= -TempTrendDeadbandC ? TempTrend.Falling
            : TempTrend.Steady;

        // Nothing to say yet if the window is too short to trust, or if the sim
        // isn't reporting wetness at all (Unknown on older builds / dry-only
        // content) - degrade to a hidden strip rather than a confident "steady".
        if (span < MinObservationSeconds
            || wetness == TrackWetness.Unknown
            || reference.Wetness == TrackWetness.Unknown)
        {
            return new WeatherNowcast
            {
                Trend = WeatherTrend.Insufficient,
                ShouldShow = false,
                Wetness = wetness,
                ReferenceWetness = reference.Wetness,
                ObservedSeconds = span,
                TrackTempC = trackTempC,
                AirTempC = airTempC,
                TrackTempTrend = tempTrend,
                TrackTempDeltaC = tempDelta,
            };
        }

        var wetnessDelta = (int)wetness - (int)reference.Wetness;
        var trend = wetnessDelta >= 1 ? WeatherTrend.Wetting
            : wetnessDelta <= -1 ? WeatherTrend.Drying
            : WeatherTrend.Steady;

        return new WeatherNowcast
        {
            Trend = trend,
            ShouldShow = trend is WeatherTrend.Wetting or WeatherTrend.Drying,
            Wetness = wetness,
            ReferenceWetness = reference.Wetness,
            ObservedSeconds = span,
            TrackTempC = trackTempC,
            AirTempC = airTempC,
            TrackTempTrend = tempTrend,
            TrackTempDeltaC = tempDelta,
        };
    }
}
