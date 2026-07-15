using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Standings;

/// <summary>One competitor row of the standings table.</summary>
public sealed record StandingsRow(
    int CarIdx,
    bool IsPlayer,
    int OverallPosition,
    int ClassPosition,
    bool IsClassLeader,
    string CarNumber,
    string DisplayName,
    string License,
    LicenseTier LicenseTier,
    int IRating,
    IRatingTier IRatingTier,
    string ClassShortName,
    string? ClassColorHex,
    double? BestLapSeconds,
    double? LastLapSeconds,
    double? LastDeltaSeconds,
    double? GapToClassLeaderSeconds,
    int GapLapsDown,
    double? IntervalSeconds,
    int IntervalLapsDown,
    bool IsSessionBestLap,
    bool InPits);
