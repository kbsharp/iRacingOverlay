using System.Windows;
using IRacingOverlay.App.ViewModels;

namespace IRacingOverlay.App;

/// <summary>
/// One overlay widget as the composition root deals with it: its stable settings
/// key, the name shown in the tray and settings window, its window, and the view
/// model telemetry is fanned out to.
///
/// This exists so <see cref="App"/> and <c>TrayIconService</c> iterate a list
/// rather than naming every widget individually. Before it, adding a widget meant
/// editing the view-model construction, four separate telemetry event handlers,
/// the scalable-window list, and the tray service's positional parameters - five
/// places, none of which the compiler would remind you about.
/// </summary>
/// <param name="Id">Stable settings key - see <c>WidgetIds</c>.</param>
/// <param name="DisplayName">Human-facing label ("Standings", "Relative").</param>
/// <param name="Window">The widget's window.</param>
/// <param name="ViewModel">
/// The telemetry-consuming view model, or null for a window that isn't
/// telemetry-driven (the demo-only dev control panel).
/// </param>
/// <param name="IsConfigurable">
/// Whether the widget appears in the tray toggles and settings window. False for
/// the dev control panel: it's demo-only scaffolding, so offering the user a
/// "disable" checkbox for it would be noise. It still takes part in layout
/// persistence and scaling.
/// </param>
public sealed record OverlayWidget(
    string Id,
    string DisplayName,
    Window Window,
    OverlayViewModelBase? ViewModel,
    bool IsConfigurable = true);
