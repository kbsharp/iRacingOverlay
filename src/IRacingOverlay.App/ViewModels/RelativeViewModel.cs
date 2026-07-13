using System.Globalization;
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
    private const int SlotsPerSide = 3;

    private SessionMetadata? _metadata;

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
        var rows = new RelativeRowViewModel[SlotsPerSide * 2 + 1];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new RelativeRowViewModel();
        }

        Rows = rows;
    }

    /// <summary>Fixed slots: 3 ahead, the player in the middle, 3 behind.</summary>
    public IReadOnlyList<RelativeRowViewModel> Rows { get; }

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

    public void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        UpdateHeader(snapshot);
        UpdateRows(snapshot);
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

        TrackTempText = "TRK " + SessionFormat.Temperature(snapshot.TrackTempC);
        AirTempText = "AIR " + SessionFormat.Temperature(snapshot.AirTempC);

        HasBrakeBias = snapshot.BrakeBiasPct > 0;
        BrakeBiasText = "BB " + snapshot.BrakeBiasPct.ToString("0.0", CultureInfo.InvariantCulture);

        IsWet = snapshot.Wetness >= TrackWetness.VeryLightlyWet;
        WetnessText = SessionFormat.Wetness(snapshot.Wetness);

        IncidentsText = snapshot.IncidentCount.ToString(CultureInfo.InvariantCulture) + "x";
    }

    private void UpdateRows(TelemetrySnapshot snapshot)
    {
        var computed = RelativeCalculator.Compute(snapshot, _metadata, SlotsPerSide);

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
            var sourceIndex = playerIndex + slot - SlotsPerSide;

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
