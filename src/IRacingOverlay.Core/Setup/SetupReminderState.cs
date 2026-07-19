namespace IRacingOverlay.Core.Setup;

/// <summary>Whether the fuel widget's setup strip should currently be flashing,
/// and why.</summary>
public readonly record struct SetupReminderState(
    string SetupName,
    bool IsModified,
    bool IsRaceOrQualify,
    bool ShouldFlash);
