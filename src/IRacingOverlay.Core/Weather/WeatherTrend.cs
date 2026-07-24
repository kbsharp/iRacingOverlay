namespace IRacingOverlay.Core.Weather;

/// <summary>
/// What the track is doing right now, derived from how wet it is compared with a
/// few minutes ago. This is a <b>nowcast</b>, not a forecast: iRacing publishes no
/// future weather to anyone (see the roadmap), so the only honest thing to report
/// is the transition already underway.
/// </summary>
public enum WeatherTrend
{
    /// <summary>Not enough history yet, or the sim reports no wetness data
    /// (older builds, dry-only content). Nothing to say.</summary>
    Insufficient,

    /// <summary>Wetness hasn't moved over the window - the driver already knows
    /// what they're on, so the strip stays hidden.</summary>
    Steady,

    /// <summary>The track is wetter than it was a few minutes ago - rain is
    /// arriving, plan the crossover to wets.</summary>
    Wetting,

    /// <summary>The track is drier than it was a few minutes ago - plan the
    /// crossover back to slicks.</summary>
    Drying,
}

/// <summary>Which way track temperature has moved over the same window. Rides
/// along as secondary context on the strip; it doesn't decide visibility.</summary>
public enum TempTrend
{
    Steady,
    Rising,
    Falling,
}
