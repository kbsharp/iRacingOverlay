using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Setup;
using IRacingOverlay.Core.Strategy;
using IRacingOverlay.Core.Telemetry;
using GridLength = System.Windows.GridLength;
using GridUnitType = System.Windows.GridUnitType;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the fuel strategy: current fuel and burn, plus fuel-to-finish,
/// the margin the driver will finish with, fuel to add at the next stop, and
/// a save target for making it without stopping.
///
/// Also carries the setup readout and its start-of-session reminder, which used
/// to be a widget of its own. Both are "what is my car running", both are read
/// in the pits rather than at speed, and the standalone panel spent a whole
/// window's worth of chrome on two lines of text - so it lives here now, as a
/// strip that <see cref="OverlaySettings.ShowSetupReminder"/> can switch off.
/// </summary>
public sealed class FuelViewModel : OverlayViewModelBase
{
    private readonly FuelCalculator _fuelCalculator;
    private readonly LapTimeTracker _lapTimeTracker;
    private readonly SetupReminderTracker _setupTracker = new();
    private readonly PitLossTracker _pitLossTracker = new();

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

    private bool _hasGauge;
    private bool _showGaugeTick;
    private bool _gaugeClearsTick = true;
    private GridLength _gaugeFillWeight = new(0, GridUnitType.Star);
    private GridLength _gaugeEmptyWeight = new(1, GridUnitType.Star);
    private GridLength _gaugeTickWeight = new(0, GridUnitType.Star);
    private GridLength _gaugeTickRestWeight = new(1, GridUnitType.Star);

    private bool _hasPitExit;
    private string _pitExitPositionText = TelemetryFormat.Placeholder;
    private string _pitExitLostText = string.Empty;
    private string _pitExitCostText = string.Empty;
    private string _pitExitNeighboursText = string.Empty;

    private string _setupNameText = TelemetryFormat.Placeholder;
    private bool _isSetupModified;
    private string _sessionTypeText = "SESSION";
    private bool _isRaceOrQualify;
    private bool _shouldFlash;
    private bool _showSetupReminder = true;

    private FuelUnit _fuelUnit = FuelUnit.Liters;
    private double _safetyMarginLaps = new WidgetTuning().FuelSafetyMarginLaps;
    private SessionMetadata? _metadata;
    private SetupReminderState _setupState = new(string.Empty, false, false, false);
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

    // ---- Tank gauge --------------------------------------------------------
    //
    // Expressed as Grid star weights rather than pixel widths so the bar
    // follows the tray UI-scale and the panel width without a magic constant
    // to keep in sync.

    /// <summary>False when the sim hasn't reported a tank capacity, which hides
    /// the bar rather than scaling it against a guess.</summary>
    public bool HasGauge
    {
        get => _hasGauge;
        private set => SetProperty(ref _hasGauge, value);
    }

    /// <summary>Whether the fuel-to-finish tick is drawn yet.</summary>
    public bool ShowGaugeTick
    {
        get => _showGaugeTick;
        private set => SetProperty(ref _showGaugeTick, value);
    }

    /// <summary>Green fill when the level clears the tick, red when short - the
    /// same meaning as the margin badge beside it.</summary>
    public bool GaugeClearsTick
    {
        get => _gaugeClearsTick;
        private set => SetProperty(ref _gaugeClearsTick, value);
    }

    public GridLength GaugeFillWeight
    {
        get => _gaugeFillWeight;
        private set => SetProperty(ref _gaugeFillWeight, value);
    }

    public GridLength GaugeEmptyWeight
    {
        get => _gaugeEmptyWeight;
        private set => SetProperty(ref _gaugeEmptyWeight, value);
    }

    public GridLength GaugeTickWeight
    {
        get => _gaugeTickWeight;
        private set => SetProperty(ref _gaugeTickWeight, value);
    }

    public GridLength GaugeTickRestWeight
    {
        get => _gaugeTickRestWeight;
        private set => SetProperty(ref _gaugeTickRestWeight, value);
    }

    // ---- Setup strip -------------------------------------------------------

    public string SetupNameText
    {
        get => _setupNameText;
        private set => SetProperty(ref _setupNameText, value);
    }

    public bool IsSetupModified
    {
        get => _isSetupModified;
        private set => SetProperty(ref _isSetupModified, value);
    }

    public string SessionTypeText
    {
        get => _sessionTypeText;
        private set => SetProperty(ref _sessionTypeText, value);
    }

    public bool IsRaceOrQualify
    {
        get => _isRaceOrQualify;
        private set => SetProperty(ref _isRaceOrQualify, value);
    }

    /// <summary>Whether the setup strip is shown at all - the user's opt-out.</summary>
    public bool ShowSetupReminder
    {
        get => _showSetupReminder;
        private set => SetProperty(ref _showSetupReminder, value);
    }

    /// <summary>True for the first minute of a Qualify/Race session - drives the
    /// panel's pulse. Forced false when the reminder is switched off, so the
    /// animation can't run against a hidden strip.</summary>
    public bool ShouldFlash
    {
        get => _shouldFlash;
        private set => SetProperty(ref _shouldFlash, value);
    }

    /// <summary>
    /// Whether the pit-exit projection is shown at all. False outside a race, and
    /// false until enough stops have been observed to know what the lane costs -
    /// the strip is absent rather than showing a guessed figure.
    /// </summary>
    public bool HasPitExit
    {
        get => _hasPitExit;
        private set => SetProperty(ref _hasPitExit, value);
    }

    /// <summary>Projected class position on rejoining, e.g. "P4".</summary>
    public string PitExitPositionText
    {
        get => _pitExitPositionText;
        private set => SetProperty(ref _pitExitPositionText, value);
    }

    /// <summary>Class places the stop gives up, e.g. "▼2"; empty when it costs none.</summary>
    public string PitExitLostText
    {
        get => _pitExitLostText;
        private set => SetProperty(ref _pitExitLostText, value);
    }

    /// <summary>The learned pit loss the projection spent, e.g. "costs 29s".</summary>
    public string PitExitCostText
    {
        get => _pitExitCostText;
        private set => SetProperty(ref _pitExitCostText, value);
    }

    /// <summary>Who you'd land between, e.g. "5.0s behind #12 · 8.0s clear of #7".</summary>
    public string PitExitNeighboursText
    {
        get => _pitExitNeighboursText;
        private set => SetProperty(ref _pitExitNeighboursText, value);
    }

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public override void ApplySettings(OverlaySettings settings)
    {
        _fuelUnit = settings.Units.Fuel;
        _safetyMarginLaps = settings.Tuning.FuelSafetyMarginLaps;
        _setupTracker.FlashDurationSeconds = settings.Tuning.SetupFlashSeconds;
        ShowSetupReminder = settings.ShowSetupReminder;
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

        // The flash is gated on the setting as well as the tracker, so switching
        // the reminder off stops an already-running pulse rather than waiting for
        // the window to expire.
        ShouldFlash = _showSetupReminder && _setupState.ShouldFlash;
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        _lapTimeTracker.Update(snapshot.Lap, snapshot.SessionTimeSeconds);

        // Stateful, like the lap-time tracker: it watches for pit-road edges, so
        // it belongs here rather than in Render, which replays the same frame.
        _pitLossTracker.Update(snapshot);

        Render(snapshot, _fuelCalculator.Update(snapshot.Lap, snapshot.FuelLevelLiters));
        RenderSetup(snapshot);
    }

    /// <summary>Advances the setup reminder. Kept out of <see cref="Render"/>
    /// because the tracker is a stateful session-change detector and
    /// <see cref="ApplySettings"/> re-renders the same frame.</summary>
    private void RenderSetup(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);

        _setupState = _setupTracker.Update(
            snapshot.SessionNum,
            sessionType,
            _metadata?.PlayerSetupName ?? string.Empty,
            _metadata?.PlayerSetupIsModified ?? false,
            snapshot.SessionTimeSeconds);

        SetupNameText = SetupFormat.DisplayName(_setupState.SetupName);
        IsSetupModified = _setupState.IsModified;
        SessionTypeText = sessionType;
        IsRaceOrQualify = _setupState.IsRaceOrQualify;
        ShouldFlash = _showSetupReminder && _setupState.ShouldFlash;
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

        RenderGauge(fuel, strategy.FuelToFinishLiters);
        RenderPitExit(snapshot);
    }

    /// <summary>
    /// The projection is pure, so it re-renders happily with the rest of the frame -
    /// only the tracker feeding it a pit loss is stateful.
    /// </summary>
    private void RenderPitExit(TelemetrySnapshot snapshot)
    {
        var projection = PitExitProjector.Compute(
            snapshot, _metadata, _pitLossTracker.MedianLossSeconds);

        HasPitExit = projection.HasProjection;
        PitExitPositionText = PitExitFormat.Position(projection);
        PitExitLostText = PitExitFormat.PositionsLost(projection);
        PitExitCostText = PitExitFormat.Cost(projection);
        PitExitNeighboursText = PitExitFormat.Neighbours(projection);
    }

    private void RenderGauge(double fuelLiters, double? fuelToFinishLiters)
    {
        var gauge = FuelGaugeCalculator.Compute(
            fuelLiters, _metadata?.TankCapacityLiters ?? 0, fuelToFinishLiters);

        HasGauge = gauge.HasGauge;
        ShowGaugeTick = gauge.ShowTick;
        GaugeClearsTick = gauge.ClearsTick;
        GaugeFillWeight = new GridLength(gauge.FillFraction, GridUnitType.Star);
        GaugeEmptyWeight = new GridLength(1 - gauge.FillFraction, GridUnitType.Star);
        GaugeTickWeight = new GridLength(gauge.TickFraction, GridUnitType.Star);
        GaugeTickRestWeight = new GridLength(1 - gauge.TickFraction, GridUnitType.Star);
    }
}
