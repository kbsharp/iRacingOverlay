namespace IRacingOverlay.Core.Delta;

/// <summary>
/// Turns iRacing's own lap-delta signal into something readable at speed.
///
/// The raw number is the sim's (<c>LapDeltaToBestLap</c>) rather than a
/// reconstruction from lap distance: iRacing already interpolates it against the
/// stored reference lap, and inventing a second answer to a question the sim
/// answers would only differ from what the driver sees in the black box.
///
/// What the sim does not do is make it readable. Three things are added here:
///
/// <list type="bullet">
/// <item>a <b>hold at the line</b>. The final delta of a lap is the one moment
/// the number is worth reading in full, and it is exactly the moment the sim
/// resets it towards zero for the new lap. The last live value of the finished
/// lap is banked and shown for <see cref="HoldSeconds"/> instead.</item>
/// <item><b>suppression</b> in the pits and out of the car, where a stale delta
/// left on screen would be read as live.</item>
/// <item>a <b>deadband</b>, so a lap being driven to the reference reads as
/// neutral rather than flickering green/red on hundredths.</item>
/// </list>
/// </summary>
public sealed class DeltaCalculator
{
    /// <summary>How long the finished lap's delta stays on screen after the
    /// line. Long enough to read on the run down to the first corner, short
    /// enough to be gone before the new lap's number matters.</summary>
    public const double HoldSeconds = 5.0;

    /// <summary>Delta at which the bar is full. A second off is already a big
    /// miss; scaling further would make the useful tenths invisible.</summary>
    public const double FullScaleSeconds = 1.0;

    /// <summary>Deltas inside this read as neutral. Below about five hundredths
    /// the sign is noise, not information.</summary>
    public const double NeutralBandSeconds = 0.05;

    private int _sessionNum = -1;
    private int _lap = -1;
    private bool _hasLive;
    private double _liveSeconds;
    private bool _isHolding;
    private double _heldSeconds;
    private double _holdUntilSeconds;

    /// <summary>
    /// Folds one telemetry frame in. <paramref name="deltaSeconds"/> is the sim's
    /// signed delta and <paramref name="deltaValid"/> its companion validity flag
    /// (false until there is a reference lap to compare against).
    /// </summary>
    public DeltaReading Update(
        int sessionNum,
        int lap,
        double sessionTimeSeconds,
        double deltaSeconds,
        bool deltaValid,
        bool isOnTrack,
        bool onPitRoad)
    {
        // A new session means a new reference lap; nothing carried over is true
        // any more.
        if (sessionNum != _sessionNum)
        {
            Reset();
            _sessionNum = sessionNum;
        }

        if (lap != _lap)
        {
            // Only a lap gained counts as crossing the line - the lap counter also
            // moves when a session resets or the driver tows back to the pits.
            if (_lap >= 0 && lap > _lap && _hasLive)
            {
                _isHolding = true;
                _heldSeconds = _liveSeconds;
                _holdUntilSeconds = sessionTimeSeconds + HoldSeconds;
            }

            _lap = lap;
        }

        // Deliberately after the banking above but before anything is returned: a
        // lap that ends by peeling into the lane is an in-lap, and holding its
        // delta over a pit stop would be reporting a number about a lap the driver
        // has already stopped caring about.
        if (!isOnTrack || onPitRoad)
        {
            _hasLive = false;
            _isHolding = false;
            return DeltaReading.Empty;
        }

        _hasLive = deltaValid;
        if (deltaValid)
        {
            _liveSeconds = deltaSeconds;
        }

        if (_isHolding)
        {
            if (sessionTimeSeconds < _holdUntilSeconds)
            {
                return Reading(DeltaState.LapComplete, _heldSeconds);
            }

            _isHolding = false;
        }

        return deltaValid ? Reading(DeltaState.Live, deltaSeconds) : DeltaReading.Empty;
    }

    private void Reset()
    {
        _lap = -1;
        _hasLive = false;
        _liveSeconds = 0;
        _isHolding = false;
        _heldSeconds = 0;
        _holdUntilSeconds = 0;
    }

    private static DeltaReading Reading(DeltaState state, double seconds)
    {
        var tone = Math.Abs(seconds) <= NeutralBandSeconds
            ? DeltaTone.Neutral
            : seconds < 0
                ? DeltaTone.Faster
                : DeltaTone.Slower;

        var fraction = Math.Clamp(Math.Abs(seconds) / FullScaleSeconds, 0, 1);
        return new DeltaReading(state, seconds, tone, fraction);
    }
}
