using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// The safety chip shared by the relative and standings session strips: corners
/// per incident for this session, and an arrow for whether that is running above
/// or below the driver's own baseline.
///
/// The chip answers "is this session helping or hurting my licence?" - the
/// drive-carefully-or-push decision - without claiming a Safety Rating number.
/// iRacing has never published the CPI-to-SR conversion, so a decimal SR delta
/// would be invented; the direction is documented, so the direction is what
/// this shows. See <see cref="SafetyTracker"/> and <see cref="CpiHistory"/>.
///
/// Unlike <see cref="IRatingChipViewModel"/> this is a <b>single shared
/// instance</b> across both widgets, not one per window. The tracker accumulates
/// a persisted baseline, so two copies would commit every session to history
/// twice and halve the apparent CPI of the driver's whole record.
/// </summary>
public sealed class SafetyChipViewModel : ObservableObject
{
    private readonly SafetyTracker _tracker;
    private readonly Action<CpiHistory>? _persist;

    private CpiHistory _lastPersisted;
    private bool _hasValue;
    private bool _hasTrend;
    private bool _isClean;
    private bool _showCpiLabel;
    private string _cpiText = string.Empty;
    private string _arrow = string.Empty;
    private RatingTrend _trend = RatingTrend.Flat;

    /// <param name="history">The baseline restored from settings.</param>
    /// <param name="persist">Called when a finished session changes the baseline,
    /// so it survives a restart. Null in tests and renders.</param>
    public SafetyChipViewModel(CpiHistory? history = null, Action<CpiHistory>? persist = null)
    {
        _tracker = new SafetyTracker(history);
        _lastPersisted = _tracker.History;
        _persist = persist;
    }

    /// <summary>False until enough laps are in for CPI to mean anything.</summary>
    public bool HasValue
    {
        get => _hasValue;
        private set => SetProperty(ref _hasValue, value);
    }

    /// <summary>
    /// False until the driver has enough history for a baseline. The chip still
    /// shows the CPI figure - it's a real measurement either way - but draws no
    /// arrow, because there is nothing yet to compare against.
    /// </summary>
    public bool HasTrend
    {
        get => _hasTrend;
        private set => SetProperty(ref _hasTrend, value);
    }

    /// <summary>No incidents yet: the one case iRacing guarantees gains SR.</summary>
    public bool IsClean
    {
        get => _isClean;
        private set => SetProperty(ref _isClean, value);
    }

    /// <summary>Whether to draw the muted "CPI" unit beside the figure. Off for a
    /// clean session, where the figure is already a word.</summary>
    public bool ShowCpiLabel
    {
        get => _showCpiLabel;
        private set => SetProperty(ref _showCpiLabel, value);
    }

    /// <summary>Corners per incident this session, or "CLEAN" while there are none.</summary>
    public string CpiText
    {
        get => _cpiText;
        private set => SetProperty(ref _cpiText, value);
    }

    public string Arrow
    {
        get => _arrow;
        private set => SetProperty(ref _arrow, value);
    }

    /// <summary>Drives the chip's colour: above baseline, below, or unknown.</summary>
    public RatingTrend Trend
    {
        get => _trend;
        private set => SetProperty(ref _trend, value);
    }

    public void Update(TelemetrySnapshot snapshot, SessionMetadata? metadata)
    {
        var outlook = _tracker.Update(snapshot, metadata);
        SaveBaselineIfChanged();

        HasValue = outlook.HasValue;

        if (!outlook.HasValue)
        {
            return;
        }

        IsClean = outlook.IsClean;
        ShowCpiLabel = !outlook.IsClean;
        HasTrend = outlook.HasTrend;
        Trend = outlook.Trend;

        // A clean session has an infinite CPI; the word carries it better than
        // a symbol, and it's the state the driver most wants to protect.
        CpiText = outlook.IsClean ? "CLEAN" : RatingFormat.Cpi(outlook.SessionCpi);

        Arrow = outlook.Trend switch
        {
            RatingTrend.Up => "▲",
            RatingTrend.Down => "▼",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Banks the session in progress. Called when the sim disconnects, so a race
    /// finished on the way to the desktop still counts toward the baseline.
    /// </summary>
    public void CommitSession()
    {
        _tracker.CommitSession();
        SaveBaselineIfChanged();
        HasValue = false;
    }

    private void SaveBaselineIfChanged()
    {
        if (_tracker.History == _lastPersisted)
        {
            return;
        }

        _lastPersisted = _tracker.History;
        _persist?.Invoke(_lastPersisted);
    }
}
