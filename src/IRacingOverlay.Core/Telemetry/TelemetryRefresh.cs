namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// The overlay's telemetry update rate, in hertz, and how it maps onto the one
/// knob the SDK actually has: how many of the sim's broadcast frames to advance
/// between updates. iRacing writes <see cref="SimBroadcastHz"/> frames a second,
/// so the only rates it can deliver honestly are that number's integer divisors
/// (60, 30, 20, 15, 10 Hz). A free-form "37 Hz" would silently become one of
/// these, so the app offers the real ones (<see cref="AllowedHz"/>) instead of a
/// slider that lies about what it does.
/// </summary>
public static class TelemetryRefresh
{
    /// <summary>Frames per second iRacing writes to its shared memory.</summary>
    public const int SimBroadcastHz = 60;

    /// <summary>The default rate, and the reason it isn't lower: below 30 Hz a car
    /// alongside on the radar visibly steps rather than slides, because its blip
    /// moves continuously. The text widgets would read fine at 15; the radar sets
    /// the floor. 30 Hz is the cheapest rate at which that motion reads smoothly.</summary>
    public const int DefaultHz = 30;

    /// <summary>The rates offered, fastest first. Each is an exact divisor of
    /// <see cref="SimBroadcastHz"/>, so the number shown is the number delivered
    /// and <see cref="FramesPerUpdate"/> never has to round.</summary>
    public static IReadOnlyList<int> AllowedHz { get; } = [60, 30, 20, 15, 10];

    /// <summary>Frames to advance per update for a rate: 60 Hz → 1 (every frame),
    /// 30 → 2, 10 → 6. This is the value fed to IRSDKSharper's UpdateInterval, and
    /// the divisor the simulated source retimes its own tick to.</summary>
    public static int FramesPerUpdate(int hz) => SimBroadcastHz / Sanitize(hz);

    /// <summary>Snaps an off-list rate — a hand-edited or future-version settings
    /// file — to the nearest offered one, so the rate is always an exact divisor of
    /// the broadcast and <see cref="FramesPerUpdate"/> is never zero. Ties round
    /// toward the faster (smoother) rate, since <see cref="AllowedHz"/> is ordered
    /// fastest-first and <see cref="Enumerable.MinBy{TSource,TKey}"/> keeps the
    /// first minimum.</summary>
    public static int Sanitize(int hz) => AllowedHz.MinBy(allowed => Math.Abs((long)allowed - hz));
}
