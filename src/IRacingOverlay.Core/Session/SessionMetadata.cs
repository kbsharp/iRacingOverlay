namespace IRacingOverlay.Core.Session;

/// <summary>
/// Slow-changing session information (driver roster, session names, the
/// player's own loaded setup) parsed from the sim's session info; refreshed
/// whenever the sim re-broadcasts it. Pace cars and spectators are excluded
/// from the roster.
/// </summary>
public sealed record SessionMetadata(
    IReadOnlyDictionary<int, RosterDriver> DriversByCarIdx,
    IReadOnlyDictionary<int, string> SessionTypesByNum,
    string PlayerSetupName,
    bool PlayerSetupIsModified);
