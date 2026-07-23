using System.Windows;
using System.Windows.Media;
using IRacingOverlay.Core.Theme;
// App has UseWindowsForms on, so Color/Brush are ambiguous across the two SDKs'
// global usings (see CLAUDE.md). SolidColorBrush/RadialGradientBrush/GradientStop
// are WPF-only and need no alias.
using Color = System.Windows.Media.Color;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Applies a <see cref="PaletteVariant"/> to the app's meaning-hue resources.
///
/// The meaning colours are declared in App.xaml and reached by every widget through
/// <c>{DynamicResource ...}</c>. Swapping one out for a new brush of a different
/// colour re-resolves in every consumer at once - so the colour-blind palette flips
/// live, mid-session, with the standings, relative, fuel, delta and radar all
/// changing together on the next frame, and no restart.
///
/// It has to be a swap rather than an in-place recolour because WPF freezes brushes
/// held in a ResourceDictionary (they come back read-only), which is also why the
/// widgets bind these keys dynamically rather than statically. The colours
/// themselves are defined and tested in <see cref="MeaningPalette"/>; this maps each
/// signal to its App.xaml resource key and does the WPF write. A missing key is
/// skipped rather than thrown on.
/// </summary>
public static class PaletteService
{
    private static readonly IReadOnlyDictionary<MeaningColor, string> ResourceKeys =
        new Dictionary<MeaningColor, string>
        {
            [MeaningColor.Positive] = "Positive",
            [MeaningColor.Negative] = "Negative",
            [MeaningColor.FastestLap] = "FastestLap",
            [MeaningColor.LapAhead] = "LapAheadText",
            [MeaningColor.LapBehind] = "LapBehindText",
            [MeaningColor.RadarPlayer] = "RadarPlayer",
        };

    private const string DangerGlowKey = "DangerGlow";

    /// <summary>Re-points the meaning-hue resources in <paramref name="resources"/>
    /// (normally <c>Application.Current.Resources</c>) to the given variant.</summary>
    public static void Apply(ResourceDictionary resources, PaletteVariant variant)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var colors = MeaningPalette.For(variant);
        foreach (var (signal, key) in ResourceKeys)
        {
            if (resources.Contains(key))
            {
                var brush = new SolidColorBrush(ToMediaColor(colors[signal]));
                brush.Freeze();
                resources[key] = brush;
            }
        }

        ApplyGlow(resources, MeaningPalette.DangerGlow(variant));
    }

    private static void ApplyGlow(ResourceDictionary resources, IReadOnlyList<GlowStop> stops)
    {
        // Keep the existing brush's geometry (origin, centre, radii) - only the hue
        // ramp moves between variants, so the shape stays single-sourced in App.xaml.
        if (resources[DangerGlowKey] is not RadialGradientBrush template)
        {
            return;
        }

        var glow = new RadialGradientBrush
        {
            GradientOrigin = template.GradientOrigin,
            Center = template.Center,
            RadiusX = template.RadiusX,
            RadiusY = template.RadiusY,
            MappingMode = template.MappingMode,
            SpreadMethod = template.SpreadMethod,
        };

        foreach (var stop in stops)
        {
            glow.GradientStops.Add(new GradientStop(ToMediaColor(stop.Color), stop.Offset));
        }

        glow.Freeze();
        resources[DangerGlowKey] = glow;
    }

    private static Color ToMediaColor(Argb argb) => Color.FromArgb(argb.A, argb.R, argb.G, argb.B);
}
