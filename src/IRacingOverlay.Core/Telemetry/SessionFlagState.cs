namespace IRacingOverlay.Core.Telemetry;

/// <summary>The single flag worth showing to the driver, in priority order.</summary>
public enum SessionFlagState
{
    /// <summary>Nothing to show - green-flag running with no incident.</summary>
    None,
    Green,
    White,
    Checkered,
    Yellow,
    Blue,
    Red,
    Black,
    Meatball,
    Disqualified,
}

/// <summary>
/// Reduces iRacing's <see cref="SessionFlags"/> bitfield - many bits of which are
/// set simultaneously - to the one flag a driver actually needs to see.
/// </summary>
public static class SessionFlagResolver
{
    /// <summary>
    /// Picks the highest-priority flag in <paramref name="flags"/>. Personal flags
    /// (black, DQ, meatball) outrank track flags, because they demand action from
    /// this driver; among track flags the more restrictive wins.
    /// </summary>
    public static SessionFlagState Resolve(SessionFlags flags)
    {
        if (flags.HasFlag(SessionFlags.Disqualify))
        {
            return SessionFlagState.Disqualified;
        }

        if (flags.HasFlag(SessionFlags.Black) || flags.HasFlag(SessionFlags.Furled))
        {
            return SessionFlagState.Black;
        }

        if (flags.HasFlag(SessionFlags.Repair))
        {
            return SessionFlagState.Meatball;
        }

        if (flags.HasFlag(SessionFlags.Red))
        {
            return SessionFlagState.Red;
        }

        // Caution/waving-yellow are separate bits from the plain yellow; any of
        // them means the same thing at a glance.
        if (flags.HasFlag(SessionFlags.Yellow)
            || flags.HasFlag(SessionFlags.YellowWaving)
            || flags.HasFlag(SessionFlags.Caution)
            || flags.HasFlag(SessionFlags.CautionWaving))
        {
            return SessionFlagState.Yellow;
        }

        if (flags.HasFlag(SessionFlags.Blue))
        {
            return SessionFlagState.Blue;
        }

        if (flags.HasFlag(SessionFlags.Checkered))
        {
            return SessionFlagState.Checkered;
        }

        if (flags.HasFlag(SessionFlags.White))
        {
            return SessionFlagState.White;
        }

        // Green is only worth the space while it's actually being shown - iRacing
        // leaves the bit set for the whole green-flag run, so treat the "held"
        // (start/restart) bit as the interesting one.
        if (flags.HasFlag(SessionFlags.GreenHeld) || flags.HasFlag(SessionFlags.StartGo))
        {
            return SessionFlagState.Green;
        }

        return SessionFlagState.None;
    }

    /// <summary>Short label for the flag, or empty when there is nothing to show.</summary>
    public static string Label(SessionFlagState state) => state switch
    {
        SessionFlagState.Green => "GREEN",
        SessionFlagState.White => "WHITE",
        SessionFlagState.Checkered => "FINISH",
        SessionFlagState.Yellow => "YELLOW",
        SessionFlagState.Blue => "BLUE",
        SessionFlagState.Red => "RED",
        SessionFlagState.Black => "BLACK",
        SessionFlagState.Meatball => "REPAIR",
        SessionFlagState.Disqualified => "DQ",
        _ => string.Empty,
    };
}
