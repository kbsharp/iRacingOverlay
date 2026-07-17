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
    private readonly List<Window> _scalableWindows = [];
    private ITelemetrySource? _telemetrySource;
    private TrayIconService? _trayIcon;
    private UpdateService? _updateService;
    private SettingsService? _settingsService;
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

        _telemetrySource.TelemetryReceived += (_, snapshot) => Dispatcher.BeginInvoke(() =>
        {
            standingsViewModel.ApplyTelemetry(snapshot);
            relativeViewModel.ApplyTelemetry(snapshot);
            fuelViewModel.ApplyTelemetry(snapshot);
            setupViewModel.ApplyTelemetry(snapshot);
            radarViewModel.ApplyTelemetry(snapshot);
        });
        _telemetrySource.SessionMetadataReceived += (_, metadata) => Dispatcher.BeginInvoke(() =>
        {
            standingsViewModel.ApplySessionMetadata(metadata);
            relativeViewModel.ApplySessionMetadata(metadata);
            setupViewModel.ApplySessionMetadata(metadata);
        });
        _telemetrySource.ConnectionChanged += (_, connected) => Dispatcher.BeginInvoke(() =>
        {
            standingsViewModel.SetConnectionState(connected);
            relativeViewModel.SetConnectionState(connected);
            fuelViewModel.SetConnectionState(connected);
            setupViewModel.SetConnectionState(connected);
            radarViewModel.SetConnectionState(connected);
        });
        _telemetrySource.ErrorOccurred += (_, exception) => Dispatcher.BeginInvoke(() =>
        {
            standingsViewModel.ReportError(exception);
            relativeViewModel.ReportError(exception);
            fuelViewModel.ReportError(exception);
            setupViewModel.ReportError(exception);
            radarViewModel.ReportError(exception);
        });

        var standingsWindow = new StandingsWindow { DataContext = standingsViewModel };
        var relativeWindow = new RelativeWindow { DataContext = relativeViewModel };
        var fuelWindow = new FuelWindow { DataContext = fuelViewModel };
        var setupWindow = new SetupWindow { DataContext = setupViewModel };
        var radarWindow = new RadarWindow { DataContext = radarViewModel };
        _scalableWindows.AddRange([standingsWindow, relativeWindow, fuelWindow, setupWindow, radarWindow]);

        DevControlWindow? devControlWindow = null;
        if (_telemetrySource is IDemoControls demoControls)
        {
            var devControlViewModel = new DevControlViewModel(demoControls);
            devControlWindow = new DevControlWindow { DataContext = devControlViewModel };
            _scalableWindows.Add(devControlWindow);
        }

        MainWindow = relativeWindow;

        // Restore each widget to its saved position (falling back to the XAML
        // default when there's no entry or it's off-screen), then show it.
        // Closing is intercepted to hide-not-exit; see HideInsteadOfClose.
        foreach (var window in _scalableWindows)
        {
            window.Closing += HideInsteadOfClose;
            RestorePosition(window);
            window.Show();
        }

        // Apply the saved UI scale to every window (SizeToContent then refits),
        // then start tracking moves - wired after the scale-driven resize so it
        // doesn't record a spurious position on launch.
        SetScale(settings.Scale);
        foreach (var window in _scalableWindows)
        {
            TrackPosition(window);
        }

        _updateService = new UpdateService();
        _trayIcon = new TrayIconService(
            standingsWindow, relativeWindow, fuelWindow, setupWindow, radarWindow, devControlWindow,
            SetScaleAndSave, RequestExit, () => _ = CheckForUpdatesAsync(manual: true), settings.Scale);

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

    /// <summary>Scales every overlay window by applying a layout transform to its
    /// content root; SizeToContent then resizes the window to fit.</summary>
    public void SetScale(double scale)
    {
        foreach (var window in _scalableWindows)
        {
            if (window.Content is FrameworkElement root)
            {
                root.LayoutTransform = new ScaleTransform(scale, scale);
            }
        }
    }

    /// <summary>Applies a UI scale and persists it, so the tray's scale choice
    /// survives a restart. (Startup applies the saved scale via
    /// <see cref="SetScale"/> directly - no need to re-save what was just loaded.)</summary>
    private void SetScaleAndSave(double scale)
    {
        SetScale(scale);
        _settingsService?.SetScale(scale);
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
