using System.Globalization;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the relative view (nearest cars ahead/behind) plus the session
/// strip (session time, temps, wetness, brake bias, incidents).
/// </summary>
public sealed class RelativeViewModel : OverlayViewModelBase
{
    private readonly PaceTrendTracker _trends = new();

    private SessionMetadata? _metadata;
    private IReadOnlyList<RelativeRowViewModel> _rows = [];
    private int _slotsPerSide = new WidgetTuning().RelativeSlotsPerSide;
    private TemperatureUnit _temperatureUnit = TemperatureUnit.Celsius;
    private bool _showPaceTrend = new OverlaySettings().ShowPaceTrend;
    private TelemetrySnapshot? _lastSnapshot;

    private string _sessionTypeText = "SESSION";
    private string _sessionRemainingText = string.Empty;
    private bool _hasSessionRemaining;
    private string _trackTempText = string.Empty;
    private string _airTempText = string.Empty;
    private string _brakeBiasText = string.Empty;
    private bool _hasBrakeBias;
    private string _wetnessText = string.Empty;
    private bool _isWet;
    private string _incidentsText = "0x";
    private IncidentSeverity _incidentLevel = IncidentSeverity.Ok;
    private string _lapCounterText = string.Empty;
    private string _flagText = string.Empty;
    private bool _hasFlag;
    private Brush _flagBackground = Brushes.Transparent;
    private Brush _flagBorder = Brushes.Transparent;
    private Brush _flagForeground = Brushes.Transparent;

    public RelativeViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
        BuildRows();
    }

    /// <summary>
    /// Fixed slots: N ahead, the player in the middle, N behind. Updated in place
    /// every frame so ordering swaps don't flicker; the collection itself is only
    /// replaced when the slot count changes, which only a settings change can do.
    /// </summary>
    public IReadOnlyList<RelativeRowViewModel> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>Projected iRating change; hides itself outside a race.</summary>
    public IRatingChipViewModel IRating { get; } = new();

    /// <summary>The session label ("RACE") - the quiet half of the strip.</summary>
    public string SessionTypeText
    {
        get => _sessionTypeText;
        private set => SetProperty(ref _sessionTypeText, value);
    }

    /// <summary>Time or laps remaining - the strip's headline figure, typeset a
    /// step larger than everything around it.</summary>
    public string SessionRemainingText
    {
        get => _sessionRemainingText;
        private set => SetProperty(ref _sessionRemainingText, value);
    }

    /// <summary>False for an unlimited session with no lap count, so the strip
    /// shows the label alone rather than a stray separator.</summary>
    public bool HasSessionRemaining
    {
        get => _hasSessionRemaining;
        private set => SetProperty(ref _hasSessionRemaining, value);
    }

    public string TrackTempText
    {
        get => _trackTempText;
        private set => SetProperty(ref _trackTempText, value);
    }

    public string AirTempText
    {
        get => _airTempText;
        private set => SetProperty(ref _airTempText, value);
    }

    public string BrakeBiasText
    {
        get => _brakeBiasText;
        private set => SetProperty(ref _brakeBiasText, value);
    }

    public bool HasBrakeBias
    {
        get => _hasBrakeBias;
        private set => SetProperty(ref _hasBrakeBias, value);
    }

    public string WetnessText
    {
        get => _wetnessText;
        private set => SetProperty(ref _wetnessText, value);
    }

    public bool IsWet
    {
        get => _isWet;
        private set => SetProperty(ref _isWet, value);
    }

    public string IncidentsText
    {
        get => _incidentsText;
        private set => SetProperty(ref _incidentsText, value);
    }

    /// <summary>How close the incident count is to the session limit, so the
    /// readout can warn before the limit rather than after.</summary>
    public IncidentSeverity IncidentLevel
    {
        get => _incidentLevel;
        private set => SetProperty(ref _incidentLevel, value);
    }

    public string LapCounterText
    {
        get => _lapCounterText;
        private set => SetProperty(ref _lapCounterText, value);
    }

    public string FlagText
    {
        get => _flagText;
        private set => SetProperty(ref _flagText, value);
    }

    public bool HasFlag
    {
        get => _hasFlag;
        private set => SetProperty(ref _hasFlag, value);
    }

    public Brush FlagBackground
    {
        get => _flagBackground;
        private set => SetProperty(ref _flagBackground, value);
    }

    public Brush FlagBorder
    {
        get => _flagBorder;
        private set => SetProperty(ref _flagBorder, value);
    }

    public Brush FlagForeground
    {
        get => _flagForeground;
        private set => SetProperty(ref _flagForeground, value);
    }

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public override void ApplySettings(OverlaySettings settings)
    {
        _temperatureUnit = settings.Units.Temperature;
        _showPaceTrend = settings.ShowPaceTrend;

        if (_slotsPerSide != settings.Tuning.RelativeSlotsPerSide)
        {
            _slotsPerSide = settings.Tuning.RelativeSlotsPerSide;
            BuildRows();
        }
        else
        {
            ApplyPaceTrendVisibility();
        }

        // Re-render the last frame so the temps and the new slot count appear
        // immediately rather than on the next telemetry tick.
        if (_lastSnapshot is { } snapshot)
        {
            UpdateHeader(snapshot);
            UpdateRows(snapshot);
        }
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        UpdateHeader(snapshot);
        UpdateRows(snapshot);
    }

    // Zebra striping is fixed per slot rather than per car, so it stays stable as
    // rows update in place - which means the pattern is baked in at build time.
    private void BuildRows()
    {
        var rows = new RelativeRowViewModel[_slotsPerSide * 2 + 1];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new RelativeRowViewModel { IsAltRow = i % 2 == 1 };
        }

        Rows = rows;
        ApplyPaceTrendVisibility();
    }

    /// <summary>The column lives on the row, so the setting has to reach every slot -
    /// including ones just rebuilt by a slot-count change.</summary>
    private void ApplyPaceTrendVisibility()
    {
        foreach (var row in Rows)
        {
            row.ShowPaceTrend = _showPaceTrend;
        }
    }

    private void UpdateHeader(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);

        var header = SessionFormat.Header(
            sessionType, snapshot.SessionTimeRemainSeconds, snapshot.SessionLapsRemain);
        SessionTypeText = header.TypeText;
        SessionRemainingText = header.RemainingText;
        HasSessionRemaining = header.RemainingText.Length > 0;

        TrackTempText = "TRK " + UnitFormat.Temperature(snapshot.TrackTempC, _temperatureUnit);
        AirTempText = "AIR " + UnitFormat.Temperature(snapshot.AirTempC, _temperatureUnit);

        // The lap counter carries the race distance when there is one, so a timed
        // race shows the clock and a lap race shows both clock and "L12/25".
        LapCounterText = SessionFormat.LapCounter(snapshot.Lap, _metadata?.LapsForSession(snapshot.SessionNum));

        HasBrakeBias = snapshot.BrakeBiasPct > 0;
        BrakeBiasText = snapshot.BrakeBiasPct.ToString("0.0", CultureInfo.InvariantCulture);

        IsWet = snapshot.Wetness >= TrackWetness.VeryLightlyWet;
        WetnessText = SessionFormat.Wetness(snapshot.Wetness);

        var incidentLimit = _metadata?.IncidentLimit;
        IncidentsText = SessionFormat.Incidents(snapshot.IncidentCount, incidentLimit);
        IncidentLevel = SessionFormat.IncidentLevel(snapshot.IncidentCount, incidentLimit);

        IRating.Update(snapshot, _metadata);

        var flag = SessionFlagResolver.Resolve(snapshot.Flags);
        HasFlag = flag != SessionFlagState.None;
        FlagText = SessionFlagResolver.Label(flag);
        (FlagBackground, FlagBorder, FlagForeground) = FlagPalette.Resolve(flag);
    }

    private void UpdateRows(TelemetrySnapshot snapshot)
    {
        var computed = RelativeCalculator.Compute(snapshot, _metadata, _slotsPerSide);
        _trends.Update(snapshot, _metadata, computed);

        var playerIndex = -1;
        for (var i = 0; i < computed.Count; i++)
        {
            if (computed[i].IsPlayer)
            {
                playerIndex = i;
                break;
            }
        }

        if (playerIndex < 0)
        {
            foreach (var row in Rows)
            {
                row.Hide();
            }

            return;
        }

        // Keep the player pinned to the middle slot; pad missing neighbours.
        for (var slot = 0; slot < Rows.Count; slot++)
        {
            var sourceIndex = playerIndex + slot - _slotsPerSide;

            if (sourceIndex >= 0 && sourceIndex < computed.Count)
            {
                var row = computed[sourceIndex];
                Rows[slot].Show(row, _trends.For(row.CarIdx));
            }
            else
            {
                Rows[slot].Hide();
            }
        }
    }
}
