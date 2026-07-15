using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using IRacingOverlay.App.Services;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;

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
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var demoMode = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var connectedLabel = demoMode ? "Demo" : "Live";

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
        standingsWindow.Closing += HideInsteadOfClose;
        relativeWindow.Closing += HideInsteadOfClose;
        fuelWindow.Closing += HideInsteadOfClose;
        setupWindow.Closing += HideInsteadOfClose;
        radarWindow.Closing += HideInsteadOfClose;

        MainWindow = relativeWindow;
        standingsWindow.Show();
        relativeWindow.Show();
        fuelWindow.Show();
        setupWindow.Show();
        radarWindow.Show();
        _scalableWindows.AddRange([standingsWindow, relativeWindow, fuelWindow, setupWindow, radarWindow]);

        DevControlWindow? devControlWindow = null;
        if (_telemetrySource is IDemoControls demoControls)
        {
            var devControlViewModel = new DevControlViewModel(demoControls);
            devControlWindow = new DevControlWindow { DataContext = devControlViewModel };
            devControlWindow.Closing += HideInsteadOfClose;
            devControlWindow.Show();
            _scalableWindows.Add(devControlWindow);
        }

        _trayIcon = new TrayIconService(
            standingsWindow, relativeWindow, fuelWindow, setupWindow, radarWindow, devControlWindow,
            SetScale, RequestExit);

        _telemetrySource.Start();
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
        _trayIcon?.Dispose();
        _telemetrySource?.Dispose();
        base.OnExit(e);
    }
}
