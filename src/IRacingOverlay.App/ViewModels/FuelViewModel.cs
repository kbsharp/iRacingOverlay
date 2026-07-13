using System.Globalization;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>Presents fuel estimates plus gear/speed to the fuel widget.</summary>
public sealed class FuelViewModel : OverlayViewModelBase
{
    private readonly FuelCalculator _fuelCalculator;

    private string _gearText = "N";
    private string _speedText = "0";
    private string _fuelLevelText = TelemetryFormat.Placeholder;
    private string _averagePerLapText = TelemetryFormat.Placeholder;
    private string _lastLapText = TelemetryFormat.Placeholder;
    private string _lapsRemainingText = TelemetryFormat.Placeholder;

    public FuelViewModel(FuelCalculator fuelCalculator, string connectedLabel = "Live")
        : base(connectedLabel)
    {
        _fuelCalculator = fuelCalculator;
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
}
