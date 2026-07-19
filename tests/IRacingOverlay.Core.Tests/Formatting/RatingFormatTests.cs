using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Tests.Formatting;

public class RatingFormatTests
{
    [Theory]
    [InlineData("R 1.23", LicenseTier.Rookie)]
    [InlineData("Rookie", LicenseTier.Rookie)]
    [InlineData("D 2.10", LicenseTier.D)]
    [InlineData("C 2.77", LicenseTier.C)]
    [InlineData("B 3.44", LicenseTier.B)]
    [InlineData("A 4.99", LicenseTier.A)]
    [InlineData("Pro", LicenseTier.Pro)]
    [InlineData("a 4.99", LicenseTier.A)] // lowercase from the sim shouldn't matter
    [InlineData("Z 1.00", LicenseTier.Unknown)]
    [InlineData("", LicenseTier.Unknown)]
    [InlineData(null, LicenseTier.Unknown)]
    public void ParseLicenseTier_ReadsTheLeadingLetter(string? license, LicenseTier expected)
    {
        Assert.Equal(expected, RatingFormat.ParseLicenseTier(license));
    }

    [Theory]
    [InlineData(42, RatingTrend.Up)]
    [InlineData(-42, RatingTrend.Down)]
    [InlineData(0, RatingTrend.Flat)]
    public void ClassifyTrend_ReadsTheSign(int delta, RatingTrend expected)
    {
        Assert.Equal(expected, RatingFormat.ClassifyTrend(delta));
    }

    [Theory]
    [InlineData(42, "42")]
    [InlineData(-42, "42")]
    [InlineData(0, "0")]
    public void DeltaMagnitude_DropsTheSign_TheArrowCarriesIt(int delta, string expected)
    {
        Assert.Equal(expected, RatingFormat.DeltaMagnitude(delta));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeHexColor_NullOrBlank_ReturnsNull(string? raw)
    {
        Assert.Null(RatingFormat.NormalizeHexColor(raw));
    }

    [Fact]
    public void NormalizeHexColor_DecimalPackedInt_FormatsAsHex()
    {
        // iRacing's real-world CarClassColor format: a decimal 0xRRGGBB integer.
        Assert.Equal("#FF9933", RatingFormat.NormalizeHexColor("16750899"));
    }

    [Fact]
    public void NormalizeHexColor_White_RoundTrips()
    {
        Assert.Equal("#FFFFFF", RatingFormat.NormalizeHexColor("16777215"));
    }

    [Theory]
    [InlineData("FFCC00", "#FFCC00")]
    [InlineData("#ffcc00", "#FFCC00")]
    [InlineData("ffcc00ff", "#FFCC00")] // 8-digit ARGB/RGBA - keep the first 6
    public void NormalizeHexColor_HexString_NormalizesToUppercaseWithHash(string raw, string expected)
    {
        Assert.Equal(expected, RatingFormat.NormalizeHexColor(raw));
    }

    [Theory]
    [InlineData("not-a-color")]
    [InlineData("-5")]
    [InlineData("99999999999999")] // overflows int - not a packed colour, and not 6/8 hex chars either
    public void NormalizeHexColor_Unrecognised_ReturnsNull(string raw)
    {
        Assert.Null(RatingFormat.NormalizeHexColor(raw));
    }
}
