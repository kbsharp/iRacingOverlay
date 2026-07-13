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
    [InlineData(0, IRatingTier.Low)]
    [InlineData(1499, IRatingTier.Low)]
    [InlineData(1500, IRatingTier.Mid)]
    [InlineData(2499, IRatingTier.Mid)]
    [InlineData(2500, IRatingTier.High)]
    [InlineData(3999, IRatingTier.High)]
    [InlineData(4000, IRatingTier.Elite)]
    [InlineData(9000, IRatingTier.Elite)]
    public void ClassifyIRating_UsesInclusiveLowerBoundaries(int irating, IRatingTier expected)
    {
        Assert.Equal(expected, RatingFormat.ClassifyIRating(irating));
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
