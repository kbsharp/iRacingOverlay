using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Formatting;

/// <summary>Pure classification of iRacing's CarLeftRight signal for the radar widget.</summary>
public static class RadarFormat
{
    public static bool HasCarLeft(CarLeftRight state) => state
        is CarLeftRight.CarLeft or CarLeftRight.CarLeftRight or CarLeftRight.TwoCarsLeft;

    public static bool HasCarRight(CarLeftRight state) => state
        is CarLeftRight.CarRight or CarLeftRight.CarLeftRight or CarLeftRight.TwoCarsRight;

    public static bool HasTwoCarsLeft(CarLeftRight state) => state == CarLeftRight.TwoCarsLeft;

    public static bool HasTwoCarsRight(CarLeftRight state) => state == CarLeftRight.TwoCarsRight;

    /// <summary>True once the sim is actively reporting (i.e. not <see cref="CarLeftRight.Off"/>),
    /// regardless of whether anyone is currently detected alongside.</summary>
    public static bool IsActive(CarLeftRight state) => state != CarLeftRight.Off;
}
