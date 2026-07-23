using IRacingOverlay.Core.Telemetry;

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

    /// <summary>Per-widget on/off. Absent key = the widget's default (see
    /// <see cref="DefaultOffWidgets"/>), so adding a widget doesn't silently
    /// disable one users already had.</summary>
    public IReadOnlyDictionary<string, bool> EnabledWidgets { get; init; }
        = new Dictionary<string, bool>();

    /// <summary>Widgets that ship switched off — a fresh install, and any settings
    /// file with no explicit entry, hides them until the user opts in. The delta
    /// bar is opt-in because it restates a number iRacing already shows in its own
    /// black box; it earns a panel only for drivers who want it always up. The
    /// track map is opt-in as the least decision-dense panel — it's the shape of
    /// the field at a glance, not a call to make — and unlike the radar it doesn't
    /// hide itself, so it earns its screen space only for drivers who want it. Both
    /// stay out of the default layout (standings + relative + fuel, radar
    /// self-hiding) rather than crowding a first impression; each is one tray
    /// click away.</summary>
    private static readonly IReadOnlySet<string> DefaultOffWidgets =
        new HashSet<string> { WidgetIds.Delta, WidgetIds.TrackMap };

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

    /// <summary>How often telemetry is polled, in hertz. Throttles the sim's 60 Hz
    /// broadcast to one of <see cref="TelemetryRefresh.AllowedHz"/>; lower saves a
    /// little CPU at the cost of choppier radar motion, 60 is the smoothest. See
    /// <see cref="TelemetryRefresh.DefaultHz"/> for why 30 is the floor by default.</summary>
    public int TelemetryRefreshHz { get; init; } = TelemetryRefresh.DefaultHz;

    /// <summary>Per-widget tuning numbers fed to the Core calculators.</summary>
    public WidgetTuning Tuning { get; init; } = new();

    /// <summary>Whether the app registers itself to launch with Windows.</summary>
    public bool RunAtStartup { get; init; }

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

    /// <summary>Whether the relative shows the catch/defend trend column. Off by
    /// default: the maths is sound but the presentation isn't legible - a bare
    /// "▲ 0.2" doesn't say seconds per lap, or toward what, so it has to be
    /// taught before it means anything. Opt-in until the readout says what it is
    /// on its own. See the roadmap entry for what a legible version needs.</summary>
    public bool ShowPaceTrend { get; init; }

    /// <summary>Whether widgets stay hidden while iRacing isn't running. On by
    /// default: with <see cref="RunAtStartup"/> set, the alternative is a set of
    /// always-on-top panels sitting over the desktop all day. Turn it off to
    /// position widgets without the sim open. See
    /// <see cref="WidgetVisibility.ShouldShow"/>.</summary>
    public bool HideWhenSimClosed { get; init; } = true;

    /// <summary>Whether a widget shows: the user's explicit choice if they've made
    /// one, otherwise the widget's default — on for most, off for the opt-in ones
    /// in <see cref="DefaultOffWidgets"/>.</summary>
    public bool IsWidgetEnabled(string widgetId)
        => EnabledWidgets.TryGetValue(widgetId, out var enabled)
            ? enabled
            : !DefaultOffWidgets.Contains(widgetId);

    /// <summary>The scale to apply to a widget: its own override if it has one,
    /// otherwise the shared <see cref="Scale"/>.</summary>
    public double ScaleFor(string widgetId)
        => WidgetScales.TryGetValue(widgetId, out var scale) ? scale : Scale;

    /// <summary>False unless the user has explicitly made this widget
    /// click-through.</summary>
    public bool IsClickThrough(string widgetId)
        => ClickThroughWidgets.TryGetValue(widgetId, out var value) && value;
}
