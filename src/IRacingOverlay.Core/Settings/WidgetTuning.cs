namespace IRacingOverlay.Core.Settings;

/// <summary>
/// The per-widget numbers that used to be hardcoded constants in the calculators
/// and were each recorded as a "not configurable yet" limitation. Every value
/// here is already a parameter on its calculator with exactly these defaults, so
/// leaving the settings file untouched reproduces the previous behaviour byte for
/// byte.
///
/// <see cref="Sanitized"/> clamps rather than rejects: these come off disk and a
/// nonsense value (a hand-edited 0, a NaN) must degrade to something usable
/// instead of throwing into the calculators' argument guards at startup.
/// </summary>
public sealed record WidgetTuning
{
    /// <summary>Laps of fuel to keep in hand at the finish when computing "Add".</summary>
    public double FuelSafetyMarginLaps { get; init; } = 0.5;

    /// <summary>How long the setup widget pulses at the start of a Qualify/Race.</summary>
    public double SetupFlashSeconds { get; init; } = 60;

    /// <summary>Along-track distance within which a car appears on the radar.
    /// The radar's job ends where the mirrors' does; 40 m is roughly nine car
    /// lengths, five times the overlap zone the danger glow grades, and the
    /// relative is the right instrument for anything further back.</summary>
    public double RadarRangeMeters { get; init; } = 40;

    /// <summary>Cars shown ahead of and behind the player in the relative.</summary>
    public int RelativeSlotsPerSide { get; init; } = 3;

    /// <summary>Cars listed per class in the standings (the player is always
    /// appended if they fall outside the window). 12 is what StandingsViewModel
    /// has always passed - the calculator's own 30 default is unused, and
    /// FEATURES.md documented the 30 by mistake.</summary>
    public int StandingsMaxPerClass { get; init; } = 12;

    public WidgetTuning Sanitized() => new()
    {
        FuelSafetyMarginLaps = Clamp(FuelSafetyMarginLaps, 0, 5, 0.5),
        SetupFlashSeconds = Clamp(SetupFlashSeconds, 5, 300, 60),
        RadarRangeMeters = Clamp(RadarRangeMeters, 15, 200, 40),
        RelativeSlotsPerSide = Math.Clamp(RelativeSlotsPerSide, 1, 8),
        StandingsMaxPerClass = Math.Clamp(StandingsMaxPerClass, 5, 60),
    };

    // A non-finite value has no sensible clamp target, so it falls back to the
    // default rather than to a band edge.
    private static double Clamp(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
