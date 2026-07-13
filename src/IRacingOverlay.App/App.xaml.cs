using System.Windows;
using IRacingOverlay.App.ViewModels;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;

namespace IRacingOverlay.App;

/// <summary>
/// Composition root: builds the telemetry source and view model, marshals
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

        var viewModel = new OverlayViewModel(
            new FuelCalculator(),
            connectedLabel: demoMode ? "Demo" : "Live");

        _telemetrySource = demoMode
            ? new SimulatedTelemetrySource()
            : new IrsdkTelemetrySource();

        _telemetrySource.TelemetryReceived += (_, snapshot) =>
            Dispatcher.BeginInvoke(() => viewModel.ApplyTelemetry(snapshot));
        _telemetrySource.ConnectionChanged += (_, connected) =>
            Dispatcher.BeginInvoke(() => viewModel.SetConnectionState(connected));
        _telemetrySource.ErrorOccurred += (_, exception) =>
            Dispatcher.BeginInvoke(() => viewModel.ReportError(exception));

        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();

        _telemetrySource.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _telemetrySource?.Dispose();
        base.OnExit(e);
    }
}
