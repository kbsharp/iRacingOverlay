namespace IRacingOverlay.Core.Demo;

/// <summary>
/// The catalog of demo race types the dev panel cycles through, modelled on the
/// iRacing series raced most: single-make cups (Mazda MX-5, Porsche Cup),
/// single-class GT3 fields, and the full three-class IMSA grid. Field sizes are
/// typical grid sizes for each series; class colours are picked to read as one
/// meaning per hue (see the colour guidance in CLAUDE.md).
/// </summary>
public static class RacePresets
{
    // One shared definition per car class so colours and pace stay consistent
    // wherever a class appears (GT3 shows up in several presets).
    private static readonly RaceClass Gtp = new("GTP", "E0532E", 96.0);
    private static readonly RaceClass Lmp2 = new("LMP2", "4C7BF0", 100.5);
    private static readonly RaceClass Gtd = new("GTD", "45B36B", 106.0);
    private static readonly RaceClass Gt3 = new("GT3", "45B36B", 105.0);
    private static readonly RaceClass PorscheCup = new("PCup", "C8102E", 111.0);
    private static readonly RaceClass Mx5 = new("MX-5", "F2A73B", 132.0);

    public static IReadOnlyList<RacePreset> All { get; } =
    [
        // Default: the three-class IMSA grid, so the demo opens showing off
        // multiclass colouring, class-relative pace, and cross-class gaps.
        new RacePreset("IMSA iRacing Series", [Gtp, Lmp2, Gtd], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24),
        new RacePreset("GT Sprint Series", [Gt3], [1], playerClassIndex: 0, defaultCarCount: 20),
        new RacePreset("GT3 Challenge - Fixed", [Gt3], [1], playerClassIndex: 0, defaultCarCount: 20),
        new RacePreset("GT3 Regional Tour - Americas", [Gt3], [1], playerClassIndex: 0, defaultCarCount: 24),
        new RacePreset("Porsche Cup - Fixed", [PorscheCup], [1], playerClassIndex: 0, defaultCarCount: 24),
        new RacePreset("Global Mazda MX-5 Cup", [Mx5], [1], playerClassIndex: 0, defaultCarCount: 24),
        new RacePreset("Advanced Mazda MX-5 Cup", [Mx5], [1], playerClassIndex: 0, defaultCarCount: 20),
    ];

    public static RacePreset Default => All[0];
}
