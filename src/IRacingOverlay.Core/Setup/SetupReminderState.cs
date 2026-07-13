namespace IRacingOverlay.Core.Setup;

/// <summary>Whether the setup widget should currently be flashing, and why.</summary>
public readonly record struct SetupReminderState(
    string SetupName,
    bool IsModified,
    bool IsRaceOrQualify,
    bool ShouldFlash);
