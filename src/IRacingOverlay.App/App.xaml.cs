using System.ComponentModel;
using System.Windows;
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
    private ITelemetrySource? _telemetrySource;
    private TrayIconService? _trayIcon;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var demoMode = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var connectedLabel = demoMode ? "Demo" : "Live";

        var relativeViewModel = new RelativeViewModel(connectedLabel);
        var fuelViewModel = new FuelViewModel(new FuelCalculator(), new LapTimeTracker(), connectedLabel);

        _telemetrySource = demoMode
            ? new SimulatedTelemetrySource()
            : new IrsdkTelemetrySource();

        _telemetrySource.TelemetryReceived += (_, snapshot) => Dispatcher.BeginInvoke(() =>
        {
            relativeViewModel.ApplyTelemetry(snapshot);
            fuelViewModel.ApplyTelemetry(snapshot);
        });
        _telemetrySource.SessionMetadataReceived += (_, metadata) =>
            Dispatcher.BeginInvoke(() => relativeViewModel.ApplySessionMetadata(metadata));
        _telemetrySource.ConnectionChanged += (_, connected) => Dispatcher.BeginInvoke(() =>
        {
            relativeViewModel.SetConnectionState(connected);
            fuelViewModel.SetConnectionState(connected);
        });
        _telemetrySource.ErrorOccurred += (_, exception) => Dispatcher.BeginInvoke(() =>
        {
            relativeViewModel.ReportError(exception);
            fuelViewModel.ReportError(exception);
        });

        var relativeWindow = new RelativeWindow { DataContext = relativeViewModel };
        var fuelWindow = new FuelWindow { DataContext = fuelViewModel };
        relativeWindow.Closing += HideInsteadOfClose;
        fuelWindow.Closing += HideInsteadOfClose;

        MainWindow = relativeWindow;
        relativeWindow.Show();
        fuelWindow.Show();

        DevControlWindow? devControlWindow = null;
        if (_telemetrySource is IDemoControls demoControls)
        {
            var devControlViewModel = new DevControlViewModel(demoControls);
            devControlWindow = new DevControlWindow { DataContext = devControlViewModel };
            devControlWindow.Closing += HideInsteadOfClose;
            devControlWindow.Show();
        }

        _trayIcon = new TrayIconService(relativeWindow, fuelWindow, devControlWindow, RequestExit);

        _telemetrySource.Start();
    }

    /// <summary>The only path that actually ends the process.</summary>
    public void RequestExit()
    {
        _isExiting = true;
        Shutdown();
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
