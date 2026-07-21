namespace IRacingOverlay.Core.Session;

/// <summary>
/// Slow-changing session information (driver roster, session names, the
/// player's own loaded setup) parsed from the sim's session info; refreshed
/// whenever the sim re-broadcasts it. Pace cars and spectators are excluded
/// from the roster.
/// </summary>
/// <param name="IncidentLimit">The session's incident cap, or null when unlimited.</param>
/// <param name="SessionLapsByNum">Scheduled lap count per session number; absent
/// for timed sessions, which the sim reports as "unlimited".</param>
/// <param name="TankCapacityLiters">The player car's usable tank capacity - the
/// scale for the fuel widget's tank gauge. 0 when the sim hasn't reported it,
/// which hides the gauge rather than drawing it against a guessed maximum.</param>
/// <param name="SectorStartPcts">The lap fraction where each timing sector begins
/// (from the sim's SplitTimeInfo), ascending from 0; null when the sim hasn't
/// reported them. Lets a track position be named by its sector - see
/// <see cref="TrackSectors"/>.</param>
public sealed record SessionMetadata(
    IReadOnlyDictionary<int, RosterDriver> DriversByCarIdx,
    IReadOnlyDictionary<int, string> SessionTypesByNum,
    string PlayerSetupName,
    bool PlayerSetupIsModified,
    double TrackLengthMeters = 0,
    int? IncidentLimit = null,
    IReadOnlyDictionary<int, int>? SessionLapsByNum = null,
    double TankCapacityLiters = 0,
    IReadOnlyList<double>? SectorStartPcts = null)
{
    /// <summary>The scheduled lap count for a session, or null when it is timed.</summary>
    public int? LapsForSession(int sessionNum) =>
        SessionLapsByNum is not null && SessionLapsByNum.TryGetValue(sessionNum, out var laps)
            ? laps
            : null;
}
