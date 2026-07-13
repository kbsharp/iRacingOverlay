using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Formatting;

public class SessionFormatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(1185, "19:45")]
    [InlineData(3723, "1:02:03")]
    public void TimeRemaining_FormatsMinutesAndHours(double seconds, string expected)
    {
        Assert.Equal(expected, SessionFormat.TimeRemaining(seconds));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(604800)] // iRacing's "unlimited" sentinel
    public void TimeRemaining_UnknownOrUnlimited_ReturnsNull(double seconds)
    {
        Assert.Null(SessionFormat.TimeRemaining(seconds));
    }

    [Theory]
    [InlineData(850, "850")]
    [InlineData(1049, "1.0k")]
    [InlineData(3449, "3.4k")]
    [InlineData(11205, "11.2k")]
    public void IRating_FormatsInThousands(int irating, string expected)
    {
        Assert.Equal(expected, SessionFormat.IRating(irating));
    }

    [Theory]
    [InlineData(1.23, "+1.2")]
    [InlineData(-0.81, "-0.8")]
    [InlineData(0, "+0.0")]
    public void Delta_FormatsWithExplicitSign(double seconds, string expected)
    {
        Assert.Equal(expected, SessionFormat.Delta(seconds));
    }

    [Theory]
    [InlineData(TrackWetness.Dry, "DRY")]
    [InlineData(TrackWetness.Unknown, "DRY")]
    [InlineData(TrackWetness.MostlyDry, "DRYING")]
    [InlineData(TrackWetness.LightlyWet, "DAMP")]
    [InlineData(TrackWetness.ModeratelyWet, "WET")]
    [InlineData(TrackWetness.ExtremelyWet, "V.WET")]
    public void Wetness_MapsToShortLabels(TrackWetness wetness, string expected)
    {
        Assert.Equal(expected, SessionFormat.Wetness(wetness));
    }

    [Fact]
    public void Temperature_RoundsToWholeDegrees()
    {
        Assert.Equal("41°", SessionFormat.Temperature(40.6f));
    }
}
