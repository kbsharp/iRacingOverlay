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
    private SingleInstanceGuard? _instanceGuard;
    private bool _isExiting;

    /// <summary>
    /// Whether telemetry is currently connected, which (with the per-widget
    /// toggle) decides what's on screen - see <see cref="WidgetVisibility"/>.
    /// Starts false so a launch with iRacing closed shows nothing but the tray
    /// icon; demo mode sets it true up front, since the simulated source never
    /// raises a connection event.
    /// </summary>
    private bool _isSimConnected;

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

        // The simulated source is "connected" by definition; the live one only
        // becomes so when iRacing is actually running.
        _isSimConnected = demoMode;

        // Built first: whether this is the installed copy decides both which
        // settings file is ours and which single-instance slot we claim, and both
        // must be settled before anything reads settings or creates a window.
        _updateService = new UpdateService();
        var isInstalled = _updateService.IsInstalled;

        // A second copy of the same flavour would give the user two tray icons,
        // two stacks of widgets, and two writers on one settings file. Exit before
        // any of that exists - and before the guard is stored, so OnExit's dispose
        // can't release the running instance's mutex.
        var guard = SingleInstanceGuard.TryAcquire(isInstalled);
        if (guard is null)
        {
            _isExiting = true;
            Shutdown();
            return;
        }

        _instanceGuard = guard;

        _settingsService = new SettingsService(isInstalled);
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
            _isSimConnected = connected;

            foreach (var viewModel in viewModels)
            {
                viewModel.SetConnectionState(connected);
            }

            // Sim opened or closed: bring the widgets back or take them away.
            ApplySettings(_settingsService!.Current);
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

            if (ShouldShow(widget, settings))
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

        var result = await _updateService.CheckAndDownloadAsync();

        if (result is { Status: UpdateCheckStatus.Ready, Update: { } update })
        {
            _trayIcon?.ShowUpdateReady(update.TargetFullRelease.Version.ToString(), () => ApplyUpdate(update));
            return;
        }

        // Everything else is only worth interrupting the user for when they asked.
        // A failed check in particular must say so rather than reporting "latest":
        // a silently broken feed is indistinguishable from being up to date, which
        // is how this app shipped three releases nobody could receive.
        if (!manual)
        {
            return;
        }

        _trayIcon?.Notify("iRacing Overlay", result.Status switch
        {
            UpdateCheckStatus.UpToDate => "You're on the latest version.",
            UpdateCheckStatus.NotInstalled =>
                "Auto-update runs in the installed app - this looks like a dev or portable build.",
            _ => "Couldn't check for updates - see update.log in %LocalAppData%\\IRacingOverlay.",
        });
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
                var show = ShouldShow(widget, settings);
                if (show && !widget.Window.IsVisible)
                {
                    widget.Window.Show();
                }
                else if (!show && widget.Window.IsVisible)
                {
                    widget.Window.Hide();
                }

                WindowInterop.SetClickThrough(widget.Window, settings.IsClickThrough(widget.Id));
            }

            widget.ViewModel?.ApplySettings(settings);
        }
    }

    /// <summary>Whether a widget belongs on screen right now: the user's toggle
    /// plus, unless they've opted out, iRacing actually being open. The dev
    /// control panel isn't user-configurable and only exists in demo mode, so it
    /// bypasses the rule entirely.</summary>
    private bool ShouldShow(OverlayWidget widget, OverlaySettings settings)
        => !widget.IsConfigurable
           || WidgetVisibility.ShouldShow(
               settings.IsWidgetEnabled(widget.Id), _isSimConnected, settings.HideWhenSimClosed);

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
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }
}
