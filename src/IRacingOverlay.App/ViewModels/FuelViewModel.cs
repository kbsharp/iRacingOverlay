using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the fuel strategy: current fuel and burn, plus fuel-to-finish,
/// the margin the driver will finish with, fuel to add at the next stop, and
/// a save target for making it without stopping.
/// </summary>
public sealed class FuelViewModel : OverlayViewModelBase
{
    private readonly FuelCalculator _fuelCalculator;
    private readonly LapTimeTracker _lapTimeTracker;

    private string _fuelLevelText = TelemetryFormat.Placeholder;
    private string _fuelLapsText = TelemetryFormat.Placeholder;
    private string _averagePerLapText = TelemetryFormat.Placeholder;
    private string _lastLapText = TelemetryFormat.Placeholder;
    private string _raceLapsText = TelemetryFormat.Placeholder;
    private bool _hasStrategy;
    private string _toFinishText = TelemetryFormat.Placeholder;
    private string _marginText = TelemetryFormat.Placeholder;
    private string _marginLabel = string.Empty;
    private bool _willFinish = true;
    private string _addFuelText = TelemetryFormat.Placeholder;
    private string _saveTargetText = TelemetryFormat.Placeholder;

    public FuelViewModel(
        FuelCalculator fuelCalculator,
        LapTimeTracker lapTimeTracker,
        string connectedLabel = "Live")
        : base(connectedLabel)
    {
        _fuelCalculator = fuelCalculator;
        _lapTimeTracker = lapTimeTracker;
    }

    public string FuelLevelText
    {
        get => _fuelLevelText;
        private set => SetProperty(ref _fuelLevelText, value);
    }

    /// <summary>Laps of running left in the tank at the current burn.</summary>
    public string FuelLapsText
    {
        get => _fuelLapsText;
        private set => SetProperty(ref _fuelLapsText, value);
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

    /// <summary>Whole laps left in the race.</summary>
    public string RaceLapsText
    {
        get => _raceLapsText;
        private set => SetProperty(ref _raceLapsText, value);
    }

    /// <summary>True once fuel-to-finish can be computed (burn + race length known).</summary>
    public bool HasStrategy
    {
        get => _hasStrategy;
        private set => SetProperty(ref _hasStrategy, value);
    }

    public string ToFinishText
    {
        get => _toFinishText;
        private set => SetProperty(ref _toFinishText, value);
    }

    /// <summary>Signed laps of fuel in hand at the finish, e.g. "+2.4" / "-0.8".</summary>
    public string MarginText
    {
        get => _marginText;
        private set => SetProperty(ref _marginText, value);
    }

    public string MarginLabel
    {
        get => _marginLabel;
        private set => SetProperty(ref _marginLabel, value);
    }

    public bool WillFinish
    {
        get => _willFinish;
        private set => SetProperty(ref _willFinish, value);
    }

    public string AddFuelText
    {
        get => _addFuelText;
        private set => SetProperty(ref _addFuelText, value);
    }

    public string SaveTargetText
    {
        get => _saveTargetText;
        private set => SetProperty(ref _saveTargetText, value);
    }

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var fuel = snapshot.FuelLevelLiters;

        _lapTimeTracker.Update(snapshot.Lap, snapshot.SessionTimeSeconds);
        var estimate = _fuelCalculator.Update(snapshot.Lap, fuel);

        var raceLaps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            snapshot.SessionLapsRemain,
            snapshot.SessionTimeRemainSeconds,
            _lapTimeTracker.AverageLapTimeSeconds);
        var strategy = FuelStrategyCalculator.Compute(fuel, estimate.AverageLitersPerLap, raceLaps);

        FuelLevelText = TelemetryFormat.Liters(fuel);
        FuelLapsText = TelemetryFormat.Laps(estimate.EstimatedLapsRemaining);
        AveragePerLapText = TelemetryFormat.Liters(estimate.AverageLitersPerLap);
        LastLapText = TelemetryFormat.Liters(estimate.LastLapLiters);

        RaceLapsText = strategy.RaceLapsRemaining is { } laps
            ? ((int)laps).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : TelemetryFormat.Placeholder;

        HasStrategy = strategy.FuelToFinishLiters is not null;
        ToFinishText = TelemetryFormat.Liters(strategy.FuelToFinishLiters);
        AddFuelText = TelemetryFormat.Liters(strategy.FuelToAddLiters);
        SaveTargetText = TelemetryFormat.Liters(strategy.SaveTargetLitersPerLap);
        WillFinish = strategy.WillFinish;
        MarginText = strategy.MarginLaps is { } margin ? SessionFormat.Delta(margin) : TelemetryFormat.Placeholder;
        MarginLabel = strategy.WillFinish ? "LAPS SPARE" : "LAPS SHORT";
    }
}
