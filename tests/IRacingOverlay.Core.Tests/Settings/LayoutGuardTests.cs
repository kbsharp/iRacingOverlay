using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class LayoutGuardTests
{
    // A typical dual-monitor virtual desktop: a 1920-wide secondary to the left
    // of a 1920-wide primary, so valid X runs from -1920 to 1920.
    private static readonly LayoutBounds DualScreen = new(Left: -1920, Top: 0, Width: 3840, Height: 1080);

    [Theory]
    [InlineData(0.8)]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    public void SanitizeScale_WithinBand_IsUnchanged(double scale)
        => Assert.Equal(scale, LayoutGuard.SanitizeScale(scale));

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(-1.0)]
    [InlineData(2.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SanitizeScale_OutOfBandOrNonFinite_ResetsTo100Percent(double scale)
        => Assert.Equal(1.0, LayoutGuard.SanitizeScale(scale));

    [Theory]
    [InlineData(0.8, 0.8)]
    [InlineData(1.25, 1.25)]
    [InlineData(2.0, 2.0)]
    [InlineData(0.1, 0.8)]
    [InlineData(9.0, 2.0)]
    public void ClampScale_KeepsTheNearestLegalValue(double scale, double expected)
        => Assert.Equal(expected, LayoutGuard.ClampScale(scale));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void ClampScale_NonFinite_FallsBackTo100Percent(double scale)
        => Assert.Equal(1.0, LayoutGuard.ClampScale(scale));

    [Fact]
    public void IsOnScreen_PositionOnPrimary_IsTrue()
        => Assert.True(LayoutGuard.IsOnScreen(new WindowPosition(24, 24), DualScreen));

    [Fact]
    public void IsOnScreen_PositionOnSecondaryMonitor_IsTrue()
        => Assert.True(LayoutGuard.IsOnScreen(new WindowPosition(-1900, 500), DualScreen));

    [Fact]
    public void IsOnScreen_PositionFromRemovedMonitor_IsFalse()
    {
        // Saved on a monitor that used to sit further left; now unplugged.
        Assert.False(LayoutGuard.IsOnScreen(new WindowPosition(-3000, 500), DualScreen));
    }

    [Fact]
    public void IsOnScreen_PositionPastRightEdge_IsFalse()
        => Assert.False(LayoutGuard.IsOnScreen(new WindowPosition(1920, 500), DualScreen));

    [Fact]
    public void IsOnScreen_NonFinitePosition_IsFalse()
        => Assert.False(LayoutGuard.IsOnScreen(new WindowPosition(double.NaN, 0), DualScreen));
}
