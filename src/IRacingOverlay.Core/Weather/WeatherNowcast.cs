using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Weather;

/// <summary>
/// One reading of the weather nowcast: the transition underway (if any), the
/// current conditions, and how far back the comparison reaches. Produced by
/// <see cref="WeatherNowcaster"/>; rendered by the weather strip.
///
/// Everything here is <b>observed</b>, never predicted - the wetness and temps
/// are current values, and <see cref="ReferenceWetness"/> / the temp delta are
/// what was measured <see cref="ObservedSeconds"/> ago. Nothing extrapolates a
/// future value, so every number is checkable a few minutes later.
/// </summary>
public sealed record WeatherNowcast
{
    public required WeatherTrend Trend { get; init; }

    /// <summary>Whether the strip belongs on screen: true only while a wetness
    /// transition is underway. Steady and insufficient states self-hide.</summary>
    public required bool ShouldShow { get; init; }

    /// <summary>Current track wetness.</summary>
    public required TrackWetness Wetness { get; init; }

    /// <summary>Wetness at the start of the observation window - what "the track
    /// was ... a few minutes ago" refers to.</summary>
    public required TrackWetness ReferenceWetness { get; init; }

    /// <summary>How long the trend has been observed over, in seconds (up to the
    /// nowcaster's lookback window). Names the "... N minutes ago" the reading is
    /// measured against.</summary>
    public required double ObservedSeconds { get; init; }

    public required float TrackTempC { get; init; }

    public required float AirTempC { get; init; }

    public required TempTrend TrackTempTrend { get; init; }

    /// <summary>Signed change in track temperature over <see cref="ObservedSeconds"/>,
    /// in Celsius degrees (negative = cooling).</summary>
    public required double TrackTempDeltaC { get; init; }
}
