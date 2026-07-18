namespace IRacingOverlay.Core.Demo;

/// <summary>
/// One car class within a demo <see cref="RacePreset"/>: its short name (as the
/// standings groups by), the class colour, a representative best-lap time used to
/// give the demo field realistic, class-relative pace, and the real iRacing
/// <c>CarPath</c> tokens the class fields - so the demo populates the manufacturer
/// badge column with the same variety a live session would.
/// </summary>
public sealed record RaceClass(
    string ShortName,
    string ColorHex,
    double BaseLapSeconds,
    IReadOnlyList<string>? CarPaths = null)
{
    public string ShortName { get; } = string.IsNullOrWhiteSpace(ShortName)
        ? throw new ArgumentException("Class short name must be non-empty.", nameof(ShortName))
        : ShortName;

    public double BaseLapSeconds { get; } = BaseLapSeconds > 0
        ? BaseLapSeconds
        : throw new ArgumentOutOfRangeException(nameof(BaseLapSeconds), BaseLapSeconds, "Base lap must be positive.");

    /// <summary>Real <c>CarPath</c> tokens for this class, or empty for a class with no badge coverage.</summary>
    public IReadOnlyList<string> CarPaths { get; } = CarPaths ?? [];
}
