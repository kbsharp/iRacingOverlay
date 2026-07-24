using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Core.Weather;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the weather nowcast: whether the track is wetting or drying right
/// now, against how wet it was a few minutes ago, with current temps alongside.
/// The judgement - what counts as a transition, the self-healing window, the
/// graceful handling of a sim that reports no wetness - lives in
/// <see cref="WeatherNowcaster"/>; this is the shell over it.
///
/// Strictly a <b>nowcast</b>: it never names a future value, because iRacing
/// publishes no forecast (see the roadmap). It self-hides via
/// <see cref="ShouldShow"/> whenever conditions are flat, so a dry, stable race
/// never shows it.
/// </summary>
public sealed class WeatherViewModel : OverlayViewModelBase
{
    private readonly WeatherNowcaster _nowcaster = new();

    private TemperatureUnit _temperatureUnit = TemperatureUnit.Celsius;
    private WeatherNowcast? _last;

    private bool _shouldShow;
    private bool _isWetting;
    private bool _isDrying;
    private string _headlineText = string.Empty;
    private string _directionGlyph = string.Empty;
    private string _wetnessText = TelemetryFormat.Placeholder;
    private string _referenceText = string.Empty;
    private string _trackTempText = TelemetryFormat.Placeholder;
    private string _airTempText = TelemetryFormat.Placeholder;
    private bool _showTempTrend;
    private string _tempTrendGlyph = string.Empty;

    public WeatherViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    /// <summary>Whether the strip belongs on screen. Drives the root's visibility,
    /// the way the radar's <c>ShouldShow</c> does - the window stays put but its
    /// content collapses to nothing while the weather is flat.</summary>
    public bool ShouldShow
    {
        get => _shouldShow;
        private set => SetProperty(ref _shouldShow, value);
    }

    /// <summary>Track is getting wetter - drives the "wetting" arrow direction.</summary>
    public bool IsWetting
    {
        get => _isWetting;
        private set => SetProperty(ref _isWetting, value);
    }

    /// <summary>Track is drying out.</summary>
    public bool IsDrying
    {
        get => _isDrying;
        private set => SetProperty(ref _isDrying, value);
    }

    /// <summary>The headline word: WETTING or DRYING.</summary>
    public string HeadlineText
    {
        get => _headlineText;
        private set => SetProperty(ref _headlineText, value);
    }

    /// <summary>Direction arrow next to the headline: ▲ wetting, ▼ drying.</summary>
    public string DirectionGlyph
    {
        get => _directionGlyph;
        private set => SetProperty(ref _directionGlyph, value);
    }

    /// <summary>Current wetness in full, e.g. "Lightly Wet".</summary>
    public string WetnessText
    {
        get => _wetnessText;
        private set => SetProperty(ref _wetnessText, value);
    }

    /// <summary>What it was before, e.g. "was Dry 5 min ago" - the checkable
    /// referent that keeps the reading honest.</summary>
    public string ReferenceText
    {
        get => _referenceText;
        private set => SetProperty(ref _referenceText, value);
    }

    public string TrackTempText
    {
        get => _trackTempText;
        private set => SetProperty(ref _trackTempText, value);
    }

    public string AirTempText
    {
        get => _airTempText;
        private set => SetProperty(ref _airTempText, value);
    }

    /// <summary>Whether track temp has moved enough to show a trend arrow.</summary>
    public bool ShowTempTrend
    {
        get => _showTempTrend;
        private set => SetProperty(ref _showTempTrend, value);
    }

    /// <summary>Track-temp trend arrow: ▲ rising, ▼ falling.</summary>
    public string TempTrendGlyph
    {
        get => _tempTrendGlyph;
        private set => SetProperty(ref _tempTrendGlyph, value);
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var nowcast = _nowcaster.Update(
            snapshot.SessionNum,
            snapshot.SessionTimeSeconds,
            snapshot.Wetness,
            snapshot.TrackTempC,
            snapshot.AirTempC);

        _last = nowcast;
        Render(nowcast);
    }

    public override void ApplySettings(OverlaySettings settings)
    {
        _temperatureUnit = settings.Units.Temperature;

        // Re-render the last reading so a units change lands without waiting for
        // the next telemetry frame - the same pattern the relative uses.
        if (_last is { } nowcast)
        {
            Render(nowcast);
        }
    }

    private void Render(WeatherNowcast nowcast)
    {
        ShouldShow = nowcast.ShouldShow;
        IsWetting = nowcast.Trend == WeatherTrend.Wetting;
        IsDrying = nowcast.Trend == WeatherTrend.Drying;

        HeadlineText = WeatherFormat.TrendLabel(nowcast.Trend);
        DirectionGlyph = nowcast.Trend switch
        {
            WeatherTrend.Wetting => "▲",
            WeatherTrend.Drying => "▼",
            _ => string.Empty,
        };

        WetnessText = WeatherFormat.WetnessLabel(nowcast.Wetness);
        ReferenceText = nowcast.ShouldShow
            ? $"was {WeatherFormat.WetnessLabel(nowcast.ReferenceWetness)} {WeatherFormat.MinutesAgo(nowcast.ObservedSeconds)} ago"
            : string.Empty;

        TrackTempText = "TRK " + UnitFormat.Temperature(nowcast.TrackTempC, _temperatureUnit);
        AirTempText = "AIR " + UnitFormat.Temperature(nowcast.AirTempC, _temperatureUnit);

        ShowTempTrend = nowcast.TrackTempTrend != TempTrend.Steady;
        TempTrendGlyph = nowcast.TrackTempTrend switch
        {
            TempTrend.Rising => "▲",
            TempTrend.Falling => "▼",
            _ => string.Empty,
        };
    }
}
