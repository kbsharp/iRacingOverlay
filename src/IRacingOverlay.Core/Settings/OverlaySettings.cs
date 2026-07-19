using IRacingOverlay.Core.Rating;

namespace IRacingOverlay.Core.Settings;

/// <summary>A saved window position (top-left corner, in device-independent
/// pixels), keyed per widget in <see cref="OverlaySettings.Windows"/>.</summary>
public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Persisted user preferences for the overlay: which widgets are on, where they
/// sit, how big they are, display units, and the per-widget tuning numbers that
/// used to be hardcoded constants. Serialized to JSON by
/// <see cref="OverlaySettingsSerializer"/>; loaded/saved by the app's
/// SettingsService.
///
/// Every per-widget map is keyed by <see cref="WidgetIds"/> and is
/// <b>sparse</b>: a widget with no entry takes the default (enabled, global
/// scale, not click-through). That keeps an existing settings file valid as
/// widgets are added, and means a fresh install writes almost nothing.
/// </summary>
public sealed record OverlaySettings
{
    /// <summary>Shared UI scale applied to every widget without its own
    /// override (1.0 = 100%).</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>Window position by widget key. Widgets with no saved entry fall
    /// back to their default XAML position.</summary>
    public IReadOnlyDictionary<string, WindowPosition> Windows { get; init; }
        = new Dictionary<string, WindowPosition>();

    /// <summary>Per-widget on/off. Absent key = enabled, so adding a widget
    /// doesn't leave it invisible for existing users.</summary>
    public IReadOnlyDictionary<string, bool> EnabledWidgets { get; init; }
        = new Dictionary<string, bool>();

    /// <summary>Per-widget scale override. Absent key = use <see cref="Scale"/>.
    /// A standings table and a radar rarely want the same size.</summary>
    public IReadOnlyDictionary<string, double> WidgetScales { get; init; }
        = new Dictionary<string, double>();

    /// <summary>Per-widget click-through (mouse events pass to the sim). Absent
    /// key = interactive, since a click-through widget can't be dragged.</summary>
    public IReadOnlyDictionary<string, bool> ClickThroughWidgets { get; init; }
        = new Dictionary<string, bool>();

    /// <summary>Display units. Conversion happens at format time only.</summary>
    public UnitPreferences Units { get; init; } = new();

    /// <summary>Per-widget tuning numbers fed to the Core calculators.</summary>
    public WidgetTuning Tuning { get; init; } = new();

    /// <summary>Whether the app registers itself to launch with Windows.</summary>
    public bool RunAtStartup { get; init; }

    /// <summary>
    /// The driver's rolling corners-per-incident baseline. Not a preference -
    /// it's earned data, and it lives here because it has to survive a restart
    /// to be worth anything. A fresh install starts empty and the safety chip
    /// shows no direction until a few sessions have been watched.
    /// </summary>
    public CpiHistory SafetyHistory { get; init; } = CpiHistory.Empty;

    /// <summary>Whether the fuel widget shows the loaded setup and pulses at the
    /// start of a Qualify/Race session. On by default - it's the behaviour the
    /// standalone setup widget had before it was folded into the fuel panel.
    /// Off gives a fuel widget with no setup strip at all.</summary>
    public bool ShowSetupReminder { get; init; } = true;

    /// <summary>Whether the standings shows a manufacturer badge column. Off by
    /// default: the mark set is incomplete, so a field mixes vector logos with
    /// text abbreviations (the makes with no CC0 artwork) and reads as
    /// inconsistent. Opt-in until every car iRacing ships has a mark.</summary>
    public bool ShowManufacturerBadges { get; init; }

    /// <summary>Whether widgets stay hidden while iRacing isn't running. On by
    /// default: with <see cref="RunAtStartup"/> set, the alternative is a set of
    /// always-on-top panels sitting over the desktop all day. Turn it off to
    /// position widgets without the sim open. See
    /// <see cref="WidgetVisibility.ShouldShow"/>.</summary>
    public bool HideWhenSimClosed { get; init; } = true;

    /// <summary>True unless the user has explicitly switched this widget off.</summary>
    public bool IsWidgetEnabled(string widgetId)
        => !EnabledWidgets.TryGetValue(widgetId, out var enabled) || enabled;

    /// <summary>The scale to apply to a widget: its own override if it has one,
    /// otherwise the shared <see cref="Scale"/>.</summary>
    public double ScaleFor(string widgetId)
        => WidgetScales.TryGetValue(widgetId, out var scale) ? scale : Scale;

    /// <summary>False unless the user has explicitly made this widget
    /// click-through.</summary>
    public bool IsClickThrough(string widgetId)
        => ClickThroughWidgets.TryGetValue(widgetId, out var value) && value;
}
