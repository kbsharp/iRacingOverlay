namespace IRacingOverlay.Core.Setup;

/// <summary>
/// Flags a short window at the start of Qualify/Race sessions during which the
/// overlay should visually flash to remind the driver to double-check they've
/// loaded the right setup - the classic "raced on the low-fuel qualifying
/// setup" mistake. Feed it every telemetry frame via <see cref="Update"/>.
/// Not thread-safe - call from a single thread.
/// </summary>
public sealed class SetupReminderTracker
{
    private const double FlashDurationSeconds = 60;

    private int? _lastSessionNum;
    private double _sessionStartedAtSeconds;

    public SetupReminderState Update(
        int sessionNum,
        string sessionType,
        string setupName,
        bool isModified,
        double sessionTimeSeconds)
    {
        if (_lastSessionNum != sessionNum)
        {
            _lastSessionNum = sessionNum;
            _sessionStartedAtSeconds = sessionTimeSeconds;
        }

        var isRaceOrQualify = IsRaceOrQualifyType(sessionType);
        var elapsed = sessionTimeSeconds - _sessionStartedAtSeconds;
        var shouldFlash = isRaceOrQualify && elapsed is >= 0 and < FlashDurationSeconds;

        return new SetupReminderState(setupName, isModified, isRaceOrQualify, shouldFlash);
    }

    private static bool IsRaceOrQualifyType(string sessionType) =>
        !string.IsNullOrEmpty(sessionType)
        && (sessionType.Contains("Qualif", StringComparison.OrdinalIgnoreCase)
            || sessionType.Contains("Race", StringComparison.OrdinalIgnoreCase));
}
