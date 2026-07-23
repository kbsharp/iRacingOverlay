using IRacingOverlay.Core.Theme;

namespace IRacingOverlay.Core.Tests.Theme;

public class ArgbTests
{
    [Theory]
    [InlineData("#33D689", 0xFF, 0x33, 0xD6, 0x89)]
    [InlineData("33D689", 0xFF, 0x33, 0xD6, 0x89)]   // '#' optional
    [InlineData("#B8FF1F1F", 0xB8, 0xFF, 0x1F, 0x1F)] // 8-digit carries alpha
    [InlineData("#00FF0000", 0x00, 0xFF, 0x00, 0x00)]
    public void ParsesSixAndEightDigitHex(string hex, byte a, byte r, byte g, byte b)
    {
        Assert.Equal(new Argb(a, r, g, b), Argb.Parse(hex));
    }

    [Theory]
    [InlineData("#33D689")]
    [InlineData("#B8FF1F1F")]
    [InlineData("#00FF0000")]
    public void RoundTripsThroughHex(string hex)
    {
        var color = Argb.Parse(hex);
        Assert.Equal(color, Argb.Parse(color.ToHex()));
    }

    [Fact]
    public void ToHexIsAlwaysEightDigitInvariant()
    {
        Assert.Equal("#FF33D689", new Argb(0xFF, 0x33, 0xD6, 0x89).ToHex());
    }

    [Theory]
    [InlineData("#12345")]     // wrong length
    [InlineData("nothex!!")]
    public void RejectsMalformedHex(string hex)
    {
        Assert.ThrowsAny<FormatException>(() => Argb.Parse(hex));
    }
}
