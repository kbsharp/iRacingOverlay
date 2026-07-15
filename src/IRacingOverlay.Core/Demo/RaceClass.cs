namespace IRacingOverlay.Core.Demo;

/// <summary>
/// One car class within a demo <see cref="RacePreset"/>: its short name (as the
/// standings groups by), the class colour, and a representative best-lap time
/// used to give the demo field realistic, class-relative pace.
/// </summary>
public sealed record RaceClass(string ShortName, string ColorHex, double BaseLapSeconds)
{
    public string ShortName { get; } = string.IsNullOrWhiteSpace(ShortName)
        ? throw new ArgumentException("Class short name must be non-empty.", nameof(ShortName))
        : ShortName;

    public double BaseLapSeconds { get; } = BaseLapSeconds > 0
        ? BaseLapSeconds
        : throw new ArgumentOutOfRangeException(nameof(BaseLapSeconds), BaseLapSeconds, "Base lap must be positive.");
}
