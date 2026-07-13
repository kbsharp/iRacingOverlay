namespace IRacingOverlay.Core.Session;

/// <summary>A competitor from the session roster (static per session).</summary>
public sealed record RosterDriver(
    int CarIdx,
    string DisplayName,
    string CarNumber,
    int IRating,
    string License,
    float ClassEstLapTimeSeconds,
    string ClassShortName,
    string? ClassColorRaw);
