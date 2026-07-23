namespace IRacingOverlay.Core.Theme;

/// <summary>Which colour scheme the meaning-hues are drawn from.</summary>
public enum PaletteVariant
{
    /// <summary>The default RaceLab/LMU-style scheme.</summary>
    Default,

    /// <summary>One preset that re-picks every hue-only signal onto values that
    /// survive red-green colour vision deficiency - not per-deficiency modes. See
    /// <see cref="MeaningPalette"/> for what moves and why.</summary>
    ColorBlindFriendly,
}

/// <summary>
/// A signal that is carried (at least partly) by colour. Each one is a
/// single-sourced brush in App.xaml; the App layer maps these to those resource
/// keys and writes the palette's colour into the live brush.
/// </summary>
public enum MeaningColor
{
    /// <summary>Gaining / faster / ahead-of-pace (green by default).</summary>
    Positive,

    /// <summary>Losing / slower / behind-of-pace (red by default).</summary>
    Negative,

    /// <summary>The session's fastest lap, echoing iRacing's timing purple.</summary>
    FastestLap,

    /// <summary>Name tint for a car a lap ahead of you (red by default).</summary>
    LapAhead,

    /// <summary>Name tint for a car a lap behind you (blue by default).</summary>
    LapBehind,

    /// <summary>The radar's "this is you" mark (green by default).</summary>
    RadarPlayer,
}

/// <summary>One stop of the radar's proximity glow gradient.</summary>
public readonly record struct GlowStop(Argb Color, double Offset);

/// <summary>
/// The app's meaning-hues, defined once in the tested domain so the colour-blind
/// preset is a data table rather than a scatter of hex literals - and so the
/// accessibility guarantee (see <see cref="ColorVision"/> and the tests) is checked
/// against the same numbers the UI paints.
///
/// The colour-blind preset moves every <b>hue-only</b> signal onto values that hold
/// up under deuteranopia/protanopia, and deliberately leaves alone the signals that
/// carry a second channel or come from the sim: the class-colour bars/dots
/// (iRacing's own <c>CarClassColor</c>), the license chip (its letter is right
/// there), and every glyph/sign/label pairing. The gain/loss pair is the measured
/// problem - see <see cref="ColorVision"/> - so it moves furthest, onto the
/// blue↔orange axis that red-green deficiency keeps intact.
/// </summary>
public static class MeaningPalette
{
    private static readonly IReadOnlyDictionary<MeaningColor, Argb> DefaultColors =
        new Dictionary<MeaningColor, Argb>
        {
            [MeaningColor.Positive] = Argb.Parse("#33D689"),   // green
            [MeaningColor.Negative] = Argb.Parse("#FF5C6C"),   // red
            [MeaningColor.FastestLap] = Argb.Parse("#C08BFF"), // violet
            [MeaningColor.LapAhead] = Argb.Parse("#FF7A85"),   // red
            [MeaningColor.LapBehind] = Argb.Parse("#6FB4FF"),  // blue
            [MeaningColor.RadarPlayer] = Argb.Parse("#37D14A"), // green
        };

    private static readonly IReadOnlyDictionary<MeaningColor, Argb> ColorBlindColors =
        new Dictionary<MeaningColor, Argb>
        {
            // Gain/loss off the red/green axis entirely: teal vs orange, the pair
            // deuteranopia/protanopia separate most. Teal rather than plain blue so
            // "gaining" never reads as the accent/branding blue (#39A7FF) it sits by.
            [MeaningColor.Positive] = Argb.Parse("#25D3C0"),   // teal
            [MeaningColor.Negative] = Argb.Parse("#FF9538"),   // orange
            // A saturated blue-violet, not magenta: magenta (red+blue) desaturates to
            // near-grey once you're green-blind, whereas a blue-dominant violet stays
            // a clear cool highlight - and keeps iRacing's own purple association.
            // Bluer/more saturated than the default so it doesn't wash out under CVD.
            [MeaningColor.FastestLap] = Argb.Parse("#9C7CFF"), // blue-violet
            // Off red, onto amber - amber vs blue is clean under CVD.
            [MeaningColor.LapAhead] = Argb.Parse("#FFA24D"),   // amber
            // Blue survives CVD and is already distinct from the amber above.
            [MeaningColor.LapBehind] = Argb.Parse("#6FB4FF"),  // blue (unchanged)
            // Dead-centre and unambiguous; green vs the magenta-red glow is CVD-safe.
            [MeaningColor.RadarPlayer] = Argb.Parse("#37D14A"), // green (unchanged)
        };

    // The proximity glow keeps the same alpha ramp in both variants - only the hue
    // moves. Default: iRacing-red. Colour-blind: a hot orange-red. Pure red is the
    // worst possible danger hue for a protan (the L-cone loss dims long wavelengths
    // most); orange-red carries more green energy, so it stays brighter and more
    // salient under protanopia while still reading unmistakably as "hot", not the
    // amber of a caution. (Magenta was tried and rejected - it desaturates to khaki
    // under protanopia, dimmer than the red it replaced.)
    private static readonly IReadOnlyList<GlowStop> DefaultGlow =
    [
        new GlowStop(Argb.Parse("#FFFF2A2A"), 0.0),
        new GlowStop(Argb.Parse("#B8FF1F1F"), 0.45),
        new GlowStop(Argb.Parse("#00FF0000"), 1.0),
    ];

    private static readonly IReadOnlyList<GlowStop> ColorBlindGlow =
    [
        new GlowStop(Argb.Parse("#FFFF5A2A"), 0.0),
        new GlowStop(Argb.Parse("#B8FF4A1E"), 0.45),
        new GlowStop(Argb.Parse("#00FF5A2A"), 1.0),
    ];

    /// <summary>The solid meaning-hues for a variant, keyed by signal.</summary>
    public static IReadOnlyDictionary<MeaningColor, Argb> For(PaletteVariant variant) =>
        variant == PaletteVariant.ColorBlindFriendly ? ColorBlindColors : DefaultColors;

    /// <summary>The radar proximity-glow gradient stops for a variant (inner → outer).</summary>
    public static IReadOnlyList<GlowStop> DangerGlow(PaletteVariant variant) =>
        variant == PaletteVariant.ColorBlindFriendly ? ColorBlindGlow : DefaultGlow;

    /// <summary>One signal's colour in a variant.</summary>
    public static Argb Color(PaletteVariant variant, MeaningColor color) => For(variant)[color];
}
