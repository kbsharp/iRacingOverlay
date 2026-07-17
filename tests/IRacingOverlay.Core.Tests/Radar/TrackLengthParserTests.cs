using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Tests.Radar;

public class TrackLengthParserTests
{
    [Theory]
    [InlineData("3.70 km", 3700.0)]
    [InlineData("3.70km", 3700.0)]
    [InlineData("0.25 km", 250.0)]
    [InlineData("  5.891 km ", 5891.0)]
    public void ParsesKilometres(string raw, double expected)
    {
        Assert.Equal(expected, TrackLengthParser.ParseToMeters(raw), precision: 3);
    }

    [Fact]
    public void ParsesMiles()
    {
        Assert.Equal(2500.0 * 1.609344, TrackLengthParser.ParseToMeters("2.50 mi"), precision: 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("-1 km")]
    [InlineData("0 km")]
    public void ReturnsZeroForMissingOrInvalid(string? raw)
    {
        Assert.Equal(0.0, TrackLengthParser.ParseToMeters(raw));
    }
}
