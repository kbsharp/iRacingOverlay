using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents telemetry and fuel estimates to the overlay window. Must only
/// be touched from the UI thread; <see cref="App"/> marshals telemetry
/// events onto the dispatcher before calling in.
/// </summary>
public sealed class OverlayViewModel : ObservableObject
{
    private readonly FuelCalculator _fuelCalculator;
    private readonly string _connectedLabel;

    private string _connectionStatus = "Waiting for iRacing";
    private bool _isConnected;
    private string _gearText = "N";
    private string _speedText = "0";
    private string _fuelLevelText = TelemetryFormat.Placeholder;
    private string _averagePerLapText = TelemetryFormat.Placeholder;
    private string _lastLapText = TelemetryFormat.Placeholder;
    private string _lapsRemainingText = TelemetryFormat.Placeholder;

    public OverlayViewModel(FuelCalculator fuelCalculator, string connectedLabel = "Live")
    {
        _fuelCalculator = fuelCalculator;
        _connectedLabel = connectedLabel;
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public string GearText
    {
        get => _gearText;
        private set => SetProperty(ref _gearText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetProperty(ref _speedText, value);
    }

    public string FuelLevelText
    {
        get => _fuelLevelText;
        private set => SetProperty(ref _fuelLevelText, value);
    }

    public string AveragePerLapText
    {
        get => _averagePerLapText;
        private set => SetProperty(ref _averagePerLapText, value);
    }

    public string LastLapText
    {
        get => _lastLapText;
        private set => SetProperty(ref _lastLapText, value);
    }

    public string LapsRemainingText
    {
        get => _lapsRemainingText;
        private set => SetProperty(ref _lapsRemainingText, value);
    }

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var estimate = _fuelCalculator.Update(snapshot.Lap, snapshot.FuelLevelLiters);

        GearText = TelemetryFormat.Gear(snapshot.Gear);
        SpeedText = TelemetryFormat.ToKph(snapshot.SpeedMetersPerSecond)
            .ToString(CultureInfo.InvariantCulture);
        FuelLevelText = TelemetryFormat.Liters(snapshot.FuelLevelLiters);
        AveragePerLapText = TelemetryFormat.Liters(estimate.AverageLitersPerLap);
        LastLapText = TelemetryFormat.Liters(estimate.LastLapLiters);
        LapsRemainingText = TelemetryFormat.Laps(estimate.EstimatedLapsRemaining);
    }

    public void SetConnectionState(bool connected)
    {
        IsConnected = connected;
        ConnectionStatus = connected ? _connectedLabel : "Waiting for iRacing";
    }

    public void ReportError(Exception exception)
    {
        Debug.WriteLine(exception);
        ConnectionStatus = "Telemetry error";
    }
}
