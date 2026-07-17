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
    // Representative real CarPath tokens per class - the actual folder names
    // iRacing uses - so the demo's manufacturer badges match a live grid.
    private static readonly string[] Gt3Cars =
    [
        "ferrari296gt3", "porsche992rgt3", "bmwm4gt3", "mclaren720sgt3",
        "audir8lmsevo2gt3", "mercedesamgevogt3", "lamborghinihuracangt3evo",
        "chevyvettez06rgt3", "acuransxevo22gt3", "astonmartinvantagegt3",
        "lexusrcfgt3", "fordmustanggt3",
    ];

    private static readonly RaceClass Gtp = new("GTP", "E0532E", 96.0,
        ["cadillacvseriesrgtp", "porsche963gtp", "bmwmhybridv8gtp", "acuraarx06gtp", "ferrari499p"]);
    private static readonly RaceClass Lmp2 = new("LMP2", "4C7BF0", 100.5, ["dallarap217"]);
    private static readonly RaceClass Gtd = new("GTD", "45B36B", 106.0, Gt3Cars);
    private static readonly RaceClass Gt3 = new("GT3", "45B36B", 105.0, Gt3Cars);
    private static readonly RaceClass PorscheCup = new("PCup", "C8102E", 111.0, ["porsche992cup"]);
    private static readonly RaceClass Mx5 = new("MX-5", "F2A73B", 132.0, ["mx5 mx52016"]);

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
