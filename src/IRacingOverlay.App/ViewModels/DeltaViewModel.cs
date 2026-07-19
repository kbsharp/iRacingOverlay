using IRacingOverlay.Core.Delta;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;
using GridLength = System.Windows.GridLength;
using GridUnitType = System.Windows.GridUnitType;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the lap delta: how far up or down the current lap is against the
/// driver's best of the session, as a signed number and a bar that grows from
/// the centre. The judgement - holding the finished lap's number at the line,
/// suppressing it in the pits, and the neutral deadband - lives in
/// <see cref="DeltaCalculator"/>; this is the shell over it.
/// </summary>
public sealed class DeltaViewModel : OverlayViewModelBase
{
    private readonly DeltaCalculator _calculator = new();

    private string _deltaText = TelemetryFormat.Placeholder;
    private string _referenceText = TelemetryFormat.Placeholder;
    private DeltaTone _tone = DeltaTone.Neutral;
    private bool _hasDelta;
    private bool _isLapComplete;
    private bool _isFaster;
    private bool _isSlower;
    private GridLength _barFillWeight = new(0, GridUnitType.Star);
    private GridLength _barRestWeight = new(1, GridUnitType.Star);

    public DeltaViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    /// <summary>The signed delta, e.g. "-0.34". Placeholder when there is
    /// nothing to report.</summary>
    public string DeltaText
    {
        get => _deltaText;
        private set => SetProperty(ref _deltaText, value);
    }

    /// <summary>The lap being measured against, as "m:ss.fff".</summary>
    public string ReferenceText
    {
        get => _referenceText;
        private set => SetProperty(ref _referenceText, value);
    }

    /// <summary>Drives the readout's colour - see <see cref="DeltaTone"/>.</summary>
    public DeltaTone Tone
    {
        get => _tone;
        private set => SetProperty(ref _tone, value);
    }

    public bool HasDelta
    {
        get => _hasDelta;
        private set => SetProperty(ref _hasDelta, value);
    }

    /// <summary>True while the number on screen belongs to the lap just finished
    /// rather than the one in progress; the widget flags that, since otherwise a
    /// held figure is indistinguishable from a live one.</summary>
    public bool IsLapComplete
    {
        get => _isLapComplete;
        private set => SetProperty(ref _isLapComplete, value);
    }

    /// <summary>Which half of the bar fills. Taken from the sign rather than the
    /// tone, so a delta inside the deadband still grows on the correct side.</summary>
    public bool IsFaster
    {
        get => _isFaster;
        private set => SetProperty(ref _isFaster, value);
    }

    public bool IsSlower
    {
        get => _isSlower;
        private set => SetProperty(ref _isSlower, value);
    }

    /// <summary>Star weights splitting one half of the bar into filled and empty
    /// - the same technique the fuel gauge uses.</summary>
    public GridLength BarFillWeight
    {
        get => _barFillWeight;
        private set => SetProperty(ref _barFillWeight, value);
    }

    public GridLength BarRestWeight
    {
        get => _barRestWeight;
        private set => SetProperty(ref _barRestWeight, value);
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var player = snapshot.Cars.FirstOrDefault(c => c.CarIdx == snapshot.PlayerCarIdx);

        var reading = _calculator.Update(
            snapshot.SessionNum,
            snapshot.Lap,
            snapshot.SessionTimeSeconds,
            snapshot.LapDeltaToBestSeconds,
            snapshot.LapDeltaToBestValid,
            snapshot.IsOnTrack,
            player.OnPitRoad);

        HasDelta = reading.HasDelta;
        IsLapComplete = reading.State == DeltaState.LapComplete;
        Tone = reading.Tone;
        DeltaText = reading.HasDelta ? DeltaFormat.Signed(reading.Seconds) : TelemetryFormat.Placeholder;

        IsFaster = reading.HasDelta && reading.Seconds < 0;
        IsSlower = reading.HasDelta && reading.Seconds > 0;
        BarFillWeight = new GridLength(reading.BarFraction, GridUnitType.Star);
        BarRestWeight = new GridLength(1 - reading.BarFraction, GridUnitType.Star);

        // The reference is the driver's own best of the session, which is what
        // the sim's delta is measured against - shown so the number has something
        // to be a delta *of*.
        ReferenceText = StandingsFormat.LapTime(player.BestLapTimeSeconds);
    }
}
