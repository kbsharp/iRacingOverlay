using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Core.Weather;

namespace IRacingOverlay.Core.Tests.Weather;

public class WeatherNowcasterTests
{
    private const int Session = 0;
    private const float TrackTemp = 30f;
    private const float AirTemp = 22f;

    /// <summary>Feeds one constant frame at a time from t=0, returning the last reading.</summary>
    private static WeatherNowcast Feed(
        WeatherNowcaster nowcaster,
        TrackWetness wetness,
        double seconds,
        double stepSeconds = 5,
        float trackTemp = TrackTemp)
    {
        WeatherNowcast? last = null;
        for (double t = 0; t <= seconds; t += stepSeconds)
        {
            last = nowcaster.Update(Session, t, wetness, trackTemp, AirTemp);
        }

        return last!;
    }

    [Fact]
    public void FirstFrame_HasNoHistory_IsInsufficientAndHidden()
    {
        var nowcaster = new WeatherNowcaster();

        var reading = nowcaster.Update(Session, 0, TrackWetness.Dry, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
        Assert.False(reading.ShouldShow);
    }

    [Fact]
    public void BeforeMinObservation_StaysInsufficient()
    {
        var nowcaster = new WeatherNowcaster();

        // Wetness has visibly risen, but only over 30s - below the 60s floor.
        nowcaster.Update(Session, 0, TrackWetness.Dry, TrackTemp, AirTemp);
        var reading = nowcaster.Update(Session, 30, TrackWetness.LightlyWet, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
        Assert.False(reading.ShouldShow);
    }

    [Fact]
    public void SteadyDry_OverFullWindow_IsSteadyAndHidden()
    {
        var nowcaster = new WeatherNowcaster();

        var reading = Feed(nowcaster, TrackWetness.Dry, seconds: 200);

        Assert.Equal(WeatherTrend.Steady, reading.Trend);
        Assert.False(reading.ShouldShow);
    }

    [Fact]
    public void WetnessRising_ReportsWettingAndShows()
    {
        var nowcaster = new WeatherNowcaster();

        // Dry for the first two minutes, then a step up to lightly wet.
        for (double t = 0; t <= 120; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.Dry, TrackTemp, AirTemp);
        }

        var reading = nowcaster.Update(Session, 125, TrackWetness.LightlyWet, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Wetting, reading.Trend);
        Assert.True(reading.ShouldShow);
        Assert.Equal(TrackWetness.LightlyWet, reading.Wetness);
        Assert.Equal(TrackWetness.Dry, reading.ReferenceWetness);
    }

    [Fact]
    public void WetnessFalling_ReportsDryingAndShows()
    {
        var nowcaster = new WeatherNowcaster();

        for (double t = 0; t <= 120; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.VeryWet, TrackTemp, AirTemp);
        }

        var reading = nowcaster.Update(Session, 125, TrackWetness.LightlyWet, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Drying, reading.Trend);
        Assert.True(reading.ShouldShow);
    }

    [Fact]
    public void CompletedTransition_OlderThanWindow_SelfHeals()
    {
        var nowcaster = new WeatherNowcaster();

        // Rain arrives early: dry until t=60, wet from t=90 on, then stable.
        nowcaster.Update(Session, 0, TrackWetness.Dry, TrackTemp, AirTemp);
        nowcaster.Update(Session, 60, TrackWetness.Dry, TrackTemp, AirTemp);
        for (double t = 90; t <= 200; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.VeryWet, TrackTemp, AirTemp);
        }

        // While the dry reference is still in the window, it reads Wetting...
        var early = nowcaster.Update(Session, 205, TrackWetness.VeryWet, TrackTemp, AirTemp);
        Assert.Equal(WeatherTrend.Wetting, early.Trend);

        // ...but once the change is older than the 300s window, the reference has
        // slid past it and it settles back to Steady on its own.
        var late = nowcaster.Update(Session, 400, TrackWetness.VeryWet, TrackTemp, AirTemp);
        Assert.Equal(WeatherTrend.Steady, late.Trend);
        Assert.False(late.ShouldShow);
    }

    [Fact]
    public void UnknownWetness_NeverTrends()
    {
        var nowcaster = new WeatherNowcaster();

        var reading = Feed(nowcaster, TrackWetness.Unknown, seconds: 200);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
        Assert.False(reading.ShouldShow);
    }

    [Fact]
    public void ReferenceWetnessUnknown_DoesNotFabricateATrend()
    {
        var nowcaster = new WeatherNowcaster();

        // Older build reports Unknown, then a build/feature flip starts reporting
        // real wetness - the pair must not read as a Dry->Wet "transition".
        for (double t = 0; t <= 120; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.Unknown, TrackTemp, AirTemp);
        }

        var reading = nowcaster.Update(Session, 125, TrackWetness.LightlyWet, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
        Assert.False(reading.ShouldShow);
    }

    [Fact]
    public void TrackTempTrend_TracksCoolingBeyondDeadband()
    {
        var nowcaster = new WeatherNowcaster();

        nowcaster.Update(Session, 0, TrackWetness.Dry, trackTempC: 40f, AirTemp);
        for (double t = 60; t <= 130; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.Dry, trackTempC: 36f, AirTemp);
        }

        var reading = nowcaster.Update(Session, 135, TrackWetness.Dry, trackTempC: 36f, AirTemp);

        Assert.Equal(TempTrend.Falling, reading.TrackTempTrend);
        Assert.True(reading.TrackTempDeltaC < 0);
    }

    [Fact]
    public void TrackTempTrend_WithinDeadband_IsSteady()
    {
        var nowcaster = new WeatherNowcaster();

        nowcaster.Update(Session, 0, TrackWetness.Dry, trackTempC: 30f, AirTemp);
        var reading = Feed(nowcaster, TrackWetness.Dry, seconds: 120, trackTemp: 31f);

        Assert.Equal(TempTrend.Steady, reading.TrackTempTrend);
    }

    [Fact]
    public void ObservedSeconds_IsCappedToTheWindow()
    {
        var nowcaster = new WeatherNowcaster();

        var reading = Feed(nowcaster, TrackWetness.Dry, seconds: 600);

        Assert.True(reading.ObservedSeconds <= WeatherNowcaster.LookbackSeconds + 1e-6);
        Assert.True(reading.ObservedSeconds >= WeatherNowcaster.LookbackSeconds - 10);
    }

    [Fact]
    public void NewSession_ResetsHistory()
    {
        var nowcaster = new WeatherNowcaster();

        // A full window of wet in session 0.
        for (double t = 0; t <= 200; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.VeryWet, TrackTemp, AirTemp);
        }

        // Session 1 opens dry; the previous session's wet must not read as "drying".
        var reading = nowcaster.Update(sessionNum: 1, sessionTimeSeconds: 0, TrackWetness.Dry, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
    }

    [Fact]
    public void ClockJumpingBackwards_ResetsHistory()
    {
        var nowcaster = new WeatherNowcaster();

        for (double t = 0; t <= 200; t += 5)
        {
            nowcaster.Update(Session, t, TrackWetness.VeryWet, TrackTemp, AirTemp);
        }

        // A session restart rewinds the clock; the reading starts over rather than
        // comparing the restarted-dry track against the old wet history.
        var reading = nowcaster.Update(Session, 0, TrackWetness.Dry, TrackTemp, AirTemp);

        Assert.Equal(WeatherTrend.Insufficient, reading.Trend);
    }
}
