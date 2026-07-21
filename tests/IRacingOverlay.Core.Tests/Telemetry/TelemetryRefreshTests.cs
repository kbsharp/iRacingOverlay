using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Telemetry;

public class TelemetryRefreshTests
{
    [Fact]
    public void DefaultHz_IsAnOfferedRate()
        => Assert.Contains(TelemetryRefresh.DefaultHz, TelemetryRefresh.AllowedHz);

    [Fact]
    public void AllowedHz_AreExactDivisorsOfTheBroadcastRate()
        => Assert.All(TelemetryRefresh.AllowedHz,
            hz => Assert.Equal(0, TelemetryRefresh.SimBroadcastHz % hz));

    // The whole point of offering only divisors: the frames-per-update handed to
    // the SDK is always a whole number, so the delivered rate is the shown rate.
    [Theory]
    [InlineData(60, 1)]
    [InlineData(30, 2)]
    [InlineData(20, 3)]
    [InlineData(15, 4)]
    [InlineData(10, 6)]
    public void FramesPerUpdate_ForAnOfferedRate_IsTheExactDivisor(int hz, int expected)
        => Assert.Equal(expected, TelemetryRefresh.FramesPerUpdate(hz));

    [Fact]
    public void FramesPerUpdate_RoundTripsBackToTheRate()
        => Assert.All(TelemetryRefresh.AllowedHz,
            hz => Assert.Equal(hz, TelemetryRefresh.SimBroadcastHz / TelemetryRefresh.FramesPerUpdate(hz)));

    [Theory]
    [InlineData(60, 60)]
    [InlineData(30, 30)]
    [InlineData(10, 10)]
    public void Sanitize_LeavesAnOfferedRateAlone(int hz, int expected)
        => Assert.Equal(expected, TelemetryRefresh.Sanitize(hz));

    [Theory]
    [InlineData(58, 60)]   // nearest offered above
    [InlineData(22, 20)]   // nearest offered below
    [InlineData(13, 15)]   // between 10 and 15, closer to 15
    [InlineData(12, 10)]   // between 10 and 15, closer to 10
    public void Sanitize_SnapsAnOffListRateToTheNearestOffered(int hz, int expected)
        => Assert.Equal(expected, TelemetryRefresh.Sanitize(hz));

    [Theory]
    [InlineData(1000)]     // absurdly high
    [InlineData(1)]        // absurdly low
    [InlineData(0)]        // a hand-edited zero, which must never reach FramesPerUpdate as a divide-by-zero
    [InlineData(-5)]       // or a negative
    public void Sanitize_ClampsAnOutOfRangeRateToAnOfferedOne(int hz)
        => Assert.Contains(TelemetryRefresh.Sanitize(hz), TelemetryRefresh.AllowedHz);

    [Fact]
    public void Sanitize_OnATie_RoundsTowardTheSmootherRate()
    {
        // 25 is equidistant from 20 and 30; the faster one wins because AllowedHz
        // is ordered fastest-first and the nearest search keeps the first minimum.
        Assert.Equal(30, TelemetryRefresh.Sanitize(25));
    }

    [Fact]
    public void FramesPerUpdate_NeverDividesByZero_ForAnyInput()
        => Assert.All(new[] { 0, -1, int.MinValue, int.MaxValue },
            hz => Assert.True(TelemetryRefresh.FramesPerUpdate(hz) > 0));
}
