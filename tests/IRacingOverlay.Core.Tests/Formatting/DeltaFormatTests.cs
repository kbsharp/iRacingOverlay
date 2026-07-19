using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Tests.Formatting;

public class DeltaFormatTests
{
    [Theory]
    [InlineData(-0.34, "-0.34")]
    [InlineData(0.34, "+0.34")]
    [InlineData(0, "+0.00")]
    [InlineData(0.125, "+0.13")]   // exact midpoint, rounded away from zero
    [InlineData(-12.5, "-12.50")]
    public void FormatsSignedToTwoDecimals(double seconds, string expected)
    {
        Assert.Equal(expected, DeltaFormat.Signed(seconds));
    }

    [Fact]
    public void DoesNotProduceNegativeZero()
    {
        // Rounding happens before the sign is taken - "-0.00" reads as a fault.
        Assert.Equal("+0.00", DeltaFormat.Signed(-0.002));
    }
}
