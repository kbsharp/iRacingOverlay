namespace IRacingOverlay.Core.Radar;

/// <summary>
/// How alarmed the radar should look on each side, 0 (nothing there) to 1 (a car
/// right on your door). This is deliberately separate from the blips themselves:
/// the blips say where cars are, this says how much it matters, and the widget
/// renders it as the red proximity glow rather than expecting the driver to read
/// pixel positions at racing speed.
/// </summary>
public static class RadarDanger
{
    /// <summary>
    /// Below this much lateral separation a car is in your own lane - queued ahead or
    /// behind, not on your door. Without this floor a train of cars nose-to-tail on the
    /// racing line lights both sides permanently, which is the opposite of useful.
    /// </summary>
    public const double MinLateralMeters = 1.2;

    /// <summary>Beyond this much lateral separation a car is in another lane, not on your door.</summary>
    public const double LateralRangeMeters = 9.0;

    /// <summary>Beyond this much longitudinal offset there is no overlap to worry about.</summary>
    public const double OverlapRangeMeters = 7.0;

    /// <summary>
    /// Peak intensity across the cars on each side. Both factors fade linearly, so a
    /// car alongside and level reads 1.0 and one drifting away in either axis fades out.
    /// </summary>
    public static (double Left, double Right) Compute(IReadOnlyList<RadarBlip> blips)
    {
        ArgumentNullException.ThrowIfNull(blips);

        double left = 0, right = 0;

        foreach (var blip in blips)
        {
            var lateral = Math.Abs(blip.RightMeters);
            var longitudinal = Math.Abs(blip.ForwardMeters);

            if (lateral < MinLateralMeters
                || lateral >= LateralRangeMeters
                || longitudinal >= OverlapRangeMeters)
            {
                continue;
            }

            var lateralFactor = 1.0 - (lateral - MinLateralMeters)
                / (LateralRangeMeters - MinLateralMeters);
            var intensity = lateralFactor * (1.0 - longitudinal / OverlapRangeMeters);

            if (blip.RightMeters < 0)
            {
                left = Math.Max(left, intensity);
            }
            else
            {
                right = Math.Max(right, intensity);
            }
        }

        return (left, right);
    }
}
