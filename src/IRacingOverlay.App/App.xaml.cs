using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using IRacingOverlay.App.Services;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;
using Velopack;

namespace IRacingOverlay.App;

/// <summary>
/// Composition root: builds the telemetry source and view models, marshals
/// background-thread telemetry events onto the dispatcher, and owns the
/// source's and tray icon's lifetime.
///
/// The app runs under <see cref="ShutdownMode.OnExplicitShutdown"/>: closing
/// an overlay window (Alt+F4, a stray click) just hides it via
/// <see cref="HideInsteadOfClose"/>, so the only way to actually end the
/// process is the tray icon's Exit, which routes through
/// <see cref="RequestExit"/>. This exists because the widgets are
/// borderless/topmost with no taskbar entry - they can end up hidden behind
/// a fullscreen game, and previously closing the launching terminal was the
/// only way out.
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly List<OverlayWidget> _widgets = [];
    private ITelemetrySource? _telemetrySource;
    private TrayIconService? _trayIcon;
    private UpdateService? _updateService;
    private SettingsService? _settingsService;
    private SettingsWindow? _settingsWindow;
    private bool _isExiting;

    /// <summary>
    /// Explicit process entry point (WPF would otherwise generate one from
    /// App.xaml - see the csproj for why we opt out). Velopack must run first:
    /// on install/update/uninstall the app is relaunched with hook arguments,
    /// and <see cref="VelopackApp.Run"/> services those and exits before any
    /// window is created. A normal launch (including <c>--demo</c>) falls
    /// through to the usual startup below. Command-line args are still surfaced
    /// on <see cref="StartupEventArgs.Args"/>, so <see cref="OnStartup"/> is
    /// unaffected by owning Main here.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var demoMode = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var connectedLabel = demoMode ? "Demo" : "Live";

        _settingsService = new SettingsService();
        var settings = _settingsService.Current;

        var standingsViewModel = new StandingsViewModel(connectedLabel);
        var relativeViewModel = new RelativeViewModel(connectedLabel);
        var fuelViewModel = new FuelViewModel(new FuelCalculator(), new LapTimeTracker(), connectedLabel);
        var setupViewModel = new SetupViewModel(connectedLabel);
        var radarViewModel = new RadarViewModel(connectedLabel);

        _telemetrySource = demoMode
            ? new SimulatedTelemetrySource()
            : new IrsdkTelemetrySource();

        // The widget registry: one entry per window, replacing what used to be
        // five view models named individually across four event handlers, a
        // window list, and the tray service's parameter list.
        _widgets.AddRange(
        [
            new OverlayWidget(WidgetIds.Standings, "Standings",
                new StandingsWindow { DataContext = standingsViewModel }, standingsViewModel),
            new OverlayWidget(WidgetIds.Relative, "Relative",
                new RelativeWindow { DataContext = relativeViewModel }, relativeViewModel),
            new OverlayWidget(WidgetIds.Fuel, "Fuel",
                new FuelWindow { DataContext = fuelViewModel }, fuelViewModel),
            new OverlayWidget(WidgetIds.Setup, "Setup",
                new SetupWindow { DataContext = setupViewModel }, setupViewModel),
            new OverlayWidget(WidgetIds.Radar, "Radar",
                new RadarWindow { DataContext = radarViewModel }, radarViewModel),
        ]);

        if (_telemetrySource is IDemoControls demoControls)
        {
            // Demo-only scaffolding: scaled and positioned like a widget, but not
            // offered as a toggle, so it doesn't clutter the settings surface.
            var devControlViewModel = new DevControlViewModel(demoControls);
            _widgets.Add(new OverlayWidget(
                "DevControlWindow", "Dev Controls",
                new DevControlWindow { DataContext = devControlViewModel },
                ViewModel: null,
                IsConfigurable: false));
        }

        var viewModels = _widgets.Select(w => w.ViewModel).OfType<OverlayViewModelBase>().ToList();

        _telemetrySource.TelemetryReceived += (_, snapshot) => Dispatcher.BeginInvoke(() =>
        {
            foreach (var viewModel in viewModels)
            {
                viewModel.ApplyTelemetry(snapshot);
            }
        });
        _telemetrySource.SessionMetadataReceived += (_, metadata) => Dispatcher.BeginInvoke(() =>
        {
            foreach (var viewModel in viewModels)
            {
                viewModel.ApplySessionMetadata(metadata);
            }
        });
        _telemetrySource.ConnectionChanged += (_, connected) => Dispatcher.BeginInvoke(() =>
        {
            foreach (var viewModel in viewModels)
            {
                viewModel.SetConnectionState(connected);
            }
        });
        _telemetrySource.ErrorOccurred += (_, exception) => Dispatcher.BeginInvoke(() =>
        {
            foreach (var viewModel in viewModels)
            {
                viewModel.ReportError(exception);
            }
        });

        MainWindow = _widgets.First(w => w.Id == WidgetIds.Relative).Window;

        // Restore each widget to its saved position (falling back to the XAML
        // default when there's no entry or it's off-screen), then show it unless
        // the user has switched it off. Closing is intercepted to hide-not-exit.
        foreach (var widget in _widgets)
        {
            widget.Window.Closing += HideInsteadOfClose;
            RestorePosition(widget.Window);

            if (settings.IsWidgetEnabled(widget.Id))
            {
                widget.Window.Show();
            }
        }

        // Push the saved settings through every widget (scale, click-through) and
        // view model (units, tuning), then start tracking moves - wired after the
        // scale-driven resize so it doesn't record a spurious position on launch.
        ApplySettings(settings);
        foreach (var widget in _widgets)
        {
            TrackPosition(widget.Window);
        }

        _settingsService.Changed += (_, updated) => ApplySettings(updated);

        _updateService = new UpdateService();
        _trayIcon = new TrayIconService(
            _widgets,
            _settingsService,
            ShowSettingsWindow,
            RequestExit,
            () => _ = CheckForUpdatesAsync(manual: true));

        // Re-assert the startup entry if it's meant to be on: an auto-update can
        // move the executable, and the stored path would otherwise go stale.
        if (settings.RunAtStartup)
        {
            StartupService.SetEnabled(true);
        }

        _telemetrySource.Start();

        // Check for a new release in the background once the UI is up. Fire and
        // forget: it must never block startup, and a dev/demo (non-installed)
        // launch no-ops inside the service.
        _ = CheckForUpdatesAsync(manual: false);
    }

    /// <summary>
    /// Checks GitHub for a newer release and, if found, downloads it and reveals
    /// the tray "restart to install" action. Runs on the UI thread up to the
    /// first await; the Velopack work happens off-thread and the continuation
    /// resumes on the dispatcher, so the tray calls here are UI-thread safe.
    /// <paramref name="manual"/> distinguishes the user-triggered "Check for
    /// updates" (which gives feedback either way) from the silent startup check.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateService is null)
        {
            return;
        }

        if (!_updateService.IsInstalled)
        {
            if (manual)
            {
                _trayIcon?.Notify(
                    "iRacing Overlay",
                    "Auto-update runs in the installed app - this looks like a dev or portable build.");
            }

            return;
        }

        var update = await _updateService.CheckAndDownloadAsync();
        if (update is not null)
        {
            _trayIcon?.ShowUpdateReady(update.TargetFullRelease.Version.ToString(), () => ApplyUpdate(update));
        }
        else if (manual)
        {
            _trayIcon?.Notify("iRacing Overlay", "You're on the latest version.");
        }
    }

    /// <summary>Installs a downloaded update and restarts the app. Ends the
    /// process, so it's only ever invoked from the user's tray click.</summary>
    private void ApplyUpdate(UpdateInfo update)
    {
        _isExiting = true;              // let shutdown proceed past HideInsteadOfClose
        _settingsService?.SaveNow();    // flush layout before the process restarts
        _trayIcon?.Dispose();           // pull the tray icon before the restart
        _updateService!.ApplyAndRestart(update);
    }

    /// <summary>The only path that actually ends the process.</summary>
    public void RequestExit()
    {
        _isExiting = true;
        Shutdown();
    }

    /// <summary>
    /// Pushes the current settings at every widget: scale, visibility,
    /// click-through, and the units/tuning each view model reads. Called once at
    /// startup and again on every settings change, so there is a single code path
    /// for "make the UI match the settings" rather than one per control.
    /// </summary>
    private void ApplySettings(OverlaySettings settings)
    {
        foreach (var widget in _widgets)
        {
            if (widget.Window.Content is FrameworkElement root)
            {
                // SizeToContent then refits the window around the scaled content.
                var scale = settings.ScaleFor(widget.Id);
                root.LayoutTransform = new ScaleTransform(scale, scale);
            }

            // The dev panel isn't user-configurable, so it's never hidden or made
            // click-through by a settings change.
            if (widget.IsConfigurable)
            {
                var enabled = settings.IsWidgetEnabled(widget.Id);
                if (enabled && !widget.Window.IsVisible)
                {
                    widget.Window.Show();
                }
                else if (!enabled && widget.Window.IsVisible)
                {
                    widget.Window.Hide();
                }

                WindowInterop.SetClickThrough(widget.Window, settings.IsClickThrough(widget.Id));
            }

            widget.ViewModel?.ApplySettings(settings);
        }
    }

    /// <summary>Opens (or re-focuses) the settings window. Created lazily - most
    /// sessions never open it, and it's the one window that isn't an overlay.</summary>
    private void ShowSettingsWindow()
    {
        if (_settingsService is null)
        {
            return;
        }

        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow
            {
                DataContext = new SettingsViewModel(
                    _settingsService,
                    _widgets.Where(w => w.IsConfigurable).Select(w => (w.Id, w.DisplayName)).ToList()),
            };

            // A normal window, so closing it should just close it - it isn't an
            // overlay that needs the hide-not-exit treatment, it just needs to be
            // rebuilt next time it's opened.
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    /// <summary>Moves a window to its saved position if there is one and it still
    /// lands on a connected display; otherwise leaves the XAML default so a layout
    /// saved on since-removed hardware can't strand a widget off-screen.</summary>
    private void RestorePosition(Window window)
    {
        if (_settingsService is null
            || !_settingsService.Current.Windows.TryGetValue(WindowKey(window), out var position))
        {
            return;
        }

        var virtualScreen = new LayoutBounds(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        if (LayoutGuard.IsOnScreen(position, virtualScreen))
        {
            window.Left = position.Left;
            window.Top = position.Top;
        }
    }

    /// <summary>Persists a window's position whenever the user drags it (debounced
    /// inside the settings service).</summary>
    private void TrackPosition(Window window)
    {
        var key = WindowKey(window);
        window.LocationChanged += (_, _) => _settingsService?.SetWindowPosition(key, window.Left, window.Top);
    }

    // Stable per-widget settings key: a window's type name is fixed across runs.
    private static string WindowKey(Window window) => window.GetType().Name;

    private void HideInsteadOfClose(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
        {
            return;
        }

        e.Cancel = true;
        ((Window)sender!).Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settingsService?.SaveNow();
        _trayIcon?.Dispose();
        _telemetrySource?.Dispose();
        base.OnExit(e);
    }
}
