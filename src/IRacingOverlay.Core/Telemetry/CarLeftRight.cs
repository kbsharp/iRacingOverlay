namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// Mirrors iRacing's CarLeftRight telemetry variable - the same near-field
/// proximity signal iRacing's own built-in spotter uses. "Off" means the
/// feature isn't active yet (e.g. not on track); "Clear" means active with
/// no cars detected alongside.
/// </summary>
public enum CarLeftRight
{
    Off = 0,
    Clear = 1,
    CarLeft = 2,
    CarRight = 3,
    CarLeftRight = 4,
    TwoCarsLeft = 5,
    TwoCarsRight = 6,
}
