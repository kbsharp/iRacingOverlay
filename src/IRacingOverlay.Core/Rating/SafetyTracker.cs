using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Rating;

/// <summary>
/// Turns a stream of telemetry frames into a live corners-per-incident readout,
/// and folds each finished session into the rolling <see cref="CpiHistory"/>
/// that later sessions are judged against.
///
/// The rules that keep it honest in a real session:
///
/// <list type="bullet">
/// <item>Every session type counts, unlike the iRating tracker — practice and
/// qualifying move Safety Rating just as a race does.</item>
/// <item><b>Except offline testing</b>, which doesn't touch any rating and would
/// otherwise pour thousands of clean corners into the baseline and make every
/// real session look bad by comparison.</item>
/// <item>Nothing shows until <see cref="MinimumLaps"/> laps are complete. One
/// incident on lap one is a CPI of a dozen corners; the number swings wildly
/// early and reports nothing the driver can act on.</item>
/// <item>Corners come from the track's turn count times laps completed. iRacing
/// scores SR against a per-track <i>corner multiplier</i> tuned for complexity,
/// which the SDK doesn't expose — so this is the same shape of measurement, not
/// the same number. It stays self-consistent, which is what the comparison
/// against our own baseline needs.</item>
/// <item>A session is committed to history when the sim moves to the next one,
/// so a session in progress never pollutes the baseline it is measured against.
/// </item>
/// </list>
///
/// <see cref="Update"/> is idempotent for a repeated snapshot - the per-session
/// counters only ever ratchet upward - so the two widgets sharing one tracker
/// can both feed it the same frame without double-counting.
/// </summary>
public sealed class SafetyTracker
{
    /// <summary>Offline testing, as normalised by <see cref="SessionFormat.ShortType"/>.</summary>
    private const string TestingSessionType = "TESTING";

    /// <summary>Laps before the readout appears. Below this, CPI is noise.</summary>
    public const int MinimumLaps = 2;

    private int _sessionNum = -1;
    private int _sessionLaps;
    private double _sessionCorners;
    private int _sessionIncidents;
    private SafetyOutlook _current = SafetyOutlook.None;

    public SafetyTracker(CpiHistory? history = null) => History = history ?? CpiHistory.Empty;

    /// <summary>The rolling baseline, including every session committed so far.</summary>
    public CpiHistory History { get; private set; }

    /// <summary>The most recent outlook. Also the return value of <see cref="Update"/>.</summary>
    public SafetyOutlook Current => _current;

    public SafetyOutlook Update(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.SessionNum != _sessionNum)
        {
            CommitSession();
            _sessionNum = snapshot.SessionNum;
        }

        if (metadata is null || metadata.TrackNumTurns <= 0 || IsTesting(metadata, snapshot.SessionNum))
        {
            return _current = SafetyOutlook.None;
        }

        // Everything here ratchets. The sim reports -1 laps for a car that has
        // left the world - towed, retired, or sitting in the paddock after the
        // flag - and a session's incident count can drop away as it tears down.
        // Reading either literally would blank the chip at the end of every race
        // and, worse, bank a finished race as zero corners driven.
        _sessionLaps = Math.Max(_sessionLaps, PlayerLapsCompleted(snapshot));
        _sessionCorners = Math.Max(_sessionCorners, metadata.TrackNumTurns * (double)_sessionLaps);
        _sessionIncidents = Math.Max(_sessionIncidents, snapshot.IncidentCount);

        if (_sessionLaps < MinimumLaps)
        {
            return _current = SafetyOutlook.None with { State = SafetyOutlookState.Pending };
        }

        var cpi = _sessionIncidents > 0
            ? _sessionCorners / _sessionIncidents
            : double.PositiveInfinity;

        return _current = new SafetyOutlook(
            SafetyOutlookState.Live,
            _sessionCorners,
            _sessionIncidents,
            cpi,
            History.AverageCpi);
    }

    /// <summary>
    /// Folds the session that just ended into the baseline and clears the
    /// per-session counters. Called automatically on a session change; call it
    /// directly when the sim disconnects, so a completed race isn't lost because
    /// the driver went straight to the desktop.
    /// </summary>
    public void CommitSession()
    {
        if (_sessionCorners > 0)
        {
            History = History.Add(_sessionCorners, _sessionIncidents);
        }

        _sessionLaps = 0;
        _sessionCorners = 0;
        _sessionIncidents = 0;
        _current = SafetyOutlook.None;
    }

    private static bool IsTesting(SessionMetadata metadata, int sessionNum) =>
        SessionFormat.ShortType(SessionFormat.ResolveSessionType(metadata.SessionTypesByNum, sessionNum))
            == TestingSessionType;

    private static int PlayerLapsCompleted(TelemetrySnapshot snapshot)
    {
        foreach (var car in snapshot.Cars)
        {
            if (car.CarIdx == snapshot.PlayerCarIdx)
            {
                return car.LapsCompleted;
            }
        }

        return 0;
    }
}
