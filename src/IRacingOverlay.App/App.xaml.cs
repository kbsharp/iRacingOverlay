using System.Windows;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;

namespace IRacingOverlay.App;

/// <summary>
/// Composition root: builds the telemetry source and view models, marshals
/// background-thread telemetry events onto the dispatcher, and owns the
/// source's lifetime.
/// </summary>
public partial class App : Application
{
    private ITelemetrySource? _telemetrySource;

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

        // The relative is the app's main window: closing it exits the app.
        MainWindow = new RelativeWindow { DataContext = relativeViewModel };
        var fuelWindow = new FuelWindow { DataContext = fuelViewModel };

        MainWindow.Show();
        fuelWindow.Show();

        _telemetrySource.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _telemetrySource?.Dispose();
        base.OnExit(e);
    }
}
