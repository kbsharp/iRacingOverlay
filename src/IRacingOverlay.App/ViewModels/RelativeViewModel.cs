using System.Globalization;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the relative view (nearest cars ahead/behind) plus the session
/// strip (session time, temps, wetness, brake bias, incidents).
/// </summary>
public sealed class RelativeViewModel : OverlayViewModelBase
{
    private SessionMetadata? _metadata;
    private IReadOnlyList<RelativeRowViewModel> _rows = [];
    private int _slotsPerSide = new WidgetTuning().RelativeSlotsPerSide;
    private TemperatureUnit _temperatureUnit = TemperatureUnit.Celsius;
    private TelemetrySnapshot? _lastSnapshot;

    private string _sessionText = "SESSION";
    private string _trackTempText = string.Empty;
    private string _airTempText = string.Empty;
    private string _brakeBiasText = string.Empty;
    private bool _hasBrakeBias;
    private string _wetnessText = string.Empty;
    private bool _isWet;
    private string _incidentsText = "0x";

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

    public string SessionText
    {
        get => _sessionText;
        private set => SetProperty(ref _sessionText, value);
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

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public override void ApplySettings(OverlaySettings settings)
    {
        _temperatureUnit = settings.Units.Temperature;

        if (_slotsPerSide != settings.Tuning.RelativeSlotsPerSide)
        {
            _slotsPerSide = settings.Tuning.RelativeSlotsPerSide;
            BuildRows();
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
    }

    private void UpdateHeader(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);

        var timeRemaining = SessionFormat.TimeRemaining(snapshot.SessionTimeRemainSeconds);
        SessionText = timeRemaining is not null
            ? $"{sessionType} · {timeRemaining}"
            : snapshot.SessionLapsRemain > 0
                ? $"{sessionType} · {snapshot.SessionLapsRemain} LAPS"
                : sessionType;

        TrackTempText = "TRK " + UnitFormat.Temperature(snapshot.TrackTempC, _temperatureUnit);
        AirTempText = "AIR " + UnitFormat.Temperature(snapshot.AirTempC, _temperatureUnit);

        HasBrakeBias = snapshot.BrakeBiasPct > 0;
        BrakeBiasText = "BB " + snapshot.BrakeBiasPct.ToString("0.0", CultureInfo.InvariantCulture);

        IsWet = snapshot.Wetness >= TrackWetness.VeryLightlyWet;
        WetnessText = SessionFormat.Wetness(snapshot.Wetness);

        IncidentsText = snapshot.IncidentCount.ToString(CultureInfo.InvariantCulture) + "x";
    }

    private void UpdateRows(TelemetrySnapshot snapshot)
    {
        var computed = RelativeCalculator.Compute(snapshot, _metadata, _slotsPerSide);

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
                Rows[slot].Show(computed[sourceIndex]);
            }
            else
            {
                Rows[slot].Hide();
            }
        }
    }
}
