using IRacingOverlay.Core.Settings;
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
    private string _fuelUnitLabel = UnitFormat.FuelLabel(FuelUnit.Liters);

    private FuelUnit _fuelUnit = FuelUnit.Liters;
    private double _safetyMarginLaps = new WidgetTuning().FuelSafetyMarginLaps;
    private TelemetrySnapshot? _lastSnapshot;

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

    /// <summary>"L" or "gal" - bound by the window so the unit shown next to every
    /// figure follows the setting.</summary>
    public string FuelUnitLabel
    {
        get => _fuelUnitLabel;
        private set => SetProperty(ref _fuelUnitLabel, value);
    }

    public override void ApplySettings(OverlaySettings settings)
    {
        _fuelUnit = settings.Units.Fuel;
        _safetyMarginLaps = settings.Tuning.FuelSafetyMarginLaps;
        FuelUnitLabel = UnitFormat.FuelLabel(_fuelUnit);

        // Re-render the last frame rather than waiting up to ~66ms for the next
        // one, so the numbers change the instant the setting does. This must not
        // touch the trackers - they're stateful lap detectors, and feeding them
        // the same frame twice is exactly the sort of thing that corrupts a
        // rolling average. Only the formatting and the (pure) strategy are redone.
        if (_lastSnapshot is { } snapshot)
        {
            Render(snapshot, _fuelCalculator.Current);
        }
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        _lapTimeTracker.Update(snapshot.Lap, snapshot.SessionTimeSeconds);
        Render(snapshot, _fuelCalculator.Update(snapshot.Lap, snapshot.FuelLevelLiters));
    }

    private void Render(TelemetrySnapshot snapshot, FuelEstimate estimate)
    {
        var fuel = snapshot.FuelLevelLiters;

        var raceLaps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            snapshot.SessionLapsRemain,
            snapshot.SessionTimeRemainSeconds,
            _lapTimeTracker.AverageLapTimeSeconds);
        var strategy = FuelStrategyCalculator.Compute(
            fuel, estimate.AverageLitersPerLap, raceLaps, _safetyMarginLaps);

        FuelLevelText = UnitFormat.Fuel(fuel, _fuelUnit);
        FuelLapsText = TelemetryFormat.Laps(estimate.EstimatedLapsRemaining);
        AveragePerLapText = UnitFormat.Fuel(estimate.AverageLitersPerLap, _fuelUnit);
        LastLapText = UnitFormat.Fuel(estimate.LastLapLiters, _fuelUnit);

        RaceLapsText = strategy.RaceLapsRemaining is { } laps
            ? ((int)laps).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : TelemetryFormat.Placeholder;

        HasStrategy = strategy.FuelToFinishLiters is not null;
        ToFinishText = UnitFormat.Fuel(strategy.FuelToFinishLiters, _fuelUnit);
        AddFuelText = UnitFormat.Fuel(strategy.FuelToAddLiters, _fuelUnit);
        SaveTargetText = UnitFormat.Fuel(strategy.SaveTargetLitersPerLap, _fuelUnit);
        WillFinish = strategy.WillFinish;
        MarginText = strategy.MarginLaps is { } margin ? SessionFormat.Delta(margin) : TelemetryFormat.Placeholder;
        MarginLabel = strategy.WillFinish ? "LAPS SPARE" : "LAPS SHORT";
    }
}
