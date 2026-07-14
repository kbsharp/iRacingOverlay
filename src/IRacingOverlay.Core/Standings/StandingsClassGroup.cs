namespace IRacingOverlay.Core.Standings;

/// <summary>A car class and its ordered rows, for the class-grouped standings table.</summary>
public sealed record StandingsClassGroup(
    string ClassShortName,
    string? ClassColorHex,
    IReadOnlyList<StandingsRow> Rows);
