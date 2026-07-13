using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents iRacing's own near-field spotter signal (<see cref="CarLeftRight"/>)
/// as a blind-spot proximity radar - a car to the left/right lights up (and
/// pulses) that side, matching the same data iRacing's built-in spotter uses.
/// </summary>
public sealed class RadarViewModel : OverlayViewModelBase
{
    private bool _isActive;
    private bool _hasCarLeft;
    private bool _hasCarRight;
    private bool _hasTwoCarsLeft;
    private bool _hasTwoCarsRight;

    public RadarViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    /// <summary>False before the sim starts reporting (e.g. not yet on track).</summary>
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(IsWaitingForData));
            }
        }
    }

    /// <summary>Inverse of <see cref="IsActive"/>, for the "waiting" placeholder's Visibility binding.</summary>
    public bool IsWaitingForData => !IsActive;

    public bool HasCarLeft
    {
        get => _hasCarLeft;
        private set => SetProperty(ref _hasCarLeft, value);
    }

    public bool HasCarRight
    {
        get => _hasCarRight;
        private set => SetProperty(ref _hasCarRight, value);
    }

    public bool HasTwoCarsLeft
    {
        get => _hasTwoCarsLeft;
        private set => SetProperty(ref _hasTwoCarsLeft, value);
    }

    public bool HasTwoCarsRight
    {
        get => _hasTwoCarsRight;
        private set => SetProperty(ref _hasTwoCarsRight, value);
    }

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var state = snapshot.CarLeftRight;

        IsActive = RadarFormat.IsActive(state);
        HasCarLeft = RadarFormat.HasCarLeft(state);
        HasCarRight = RadarFormat.HasCarRight(state);
        HasTwoCarsLeft = RadarFormat.HasTwoCarsLeft(state);
        HasTwoCarsRight = RadarFormat.HasTwoCarsRight(state);
    }
}
