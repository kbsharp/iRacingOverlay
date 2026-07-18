using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Setup;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the loaded car setup and flashes for the first minute of a
/// Qualify/Race session, as a reminder to double-check it's the right one -
/// the classic "raced on the qualifying setup" mistake.
/// </summary>
public sealed class SetupViewModel : OverlayViewModelBase
{
    private readonly SetupReminderTracker _tracker = new();

    private SessionMetadata? _metadata;

    private string _setupNameText = TelemetryFormat.Placeholder;
    private bool _isModified;
    private string _sessionTypeText = "SESSION";
    private bool _isRaceOrQualify;
    private bool _shouldFlash;

    public SetupViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    public string SetupNameText
    {
        get => _setupNameText;
        private set => SetProperty(ref _setupNameText, value);
    }

    public bool IsModified
    {
        get => _isModified;
        private set => SetProperty(ref _isModified, value);
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

    /// <summary>True for the first minute of a Qualify/Race session - drives the
    /// window's flashing animation.</summary>
    public bool ShouldFlash
    {
        get => _shouldFlash;
        private set => SetProperty(ref _shouldFlash, value);
    }

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public override void ApplySettings(OverlaySettings settings)
        => _tracker.FlashDurationSeconds = settings.Tuning.SetupFlashSeconds;

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);
        var setupName = _metadata?.PlayerSetupName ?? string.Empty;
        var isModified = _metadata?.PlayerSetupIsModified ?? false;

        var state = _tracker.Update(
            snapshot.SessionNum, sessionType, setupName, isModified, snapshot.SessionTimeSeconds);

        SetupNameText = SetupFormat.DisplayName(state.SetupName);
        IsModified = state.IsModified;
        SessionTypeText = sessionType;
        IsRaceOrQualify = state.IsRaceOrQualify;
        ShouldFlash = state.ShouldFlash;
    }
}
