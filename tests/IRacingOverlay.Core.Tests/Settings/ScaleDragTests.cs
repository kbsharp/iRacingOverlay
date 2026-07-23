using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class ScaleDragTests
{
    // The standings panel's unscaled content box, near enough: a wide, short panel,
    // which is the shape that makes the two axes disagree the most.
    private const double Width = 644;
    private const double Height = 300;

    [Fact]
    public void Resize_NoMovement_KeepsTheScale()
        => Assert.Equal(1.25, ScaleDrag.Resize(1.25, Width, Height, 0, 0));

    [Fact]
    public void Resize_DraggingOutAlongTheDiagonal_TracksThePointer()
    {
        // Along the panel's own diagonal both axes agree, so the corner should land
        // exactly under the pointer: +64.4px of a 644px width is +10%.
        var scale = ScaleDrag.Resize(1.0, Width, Height, dragX: 64.4, dragY: 30.0);

        Assert.Equal(1.1, scale, precision: 3);
    }

    [Fact]
    public void Resize_DraggingIn_Shrinks()
    {
        var scale = ScaleDrag.Resize(1.5, Width, Height, dragX: -64.4, dragY: -30.0);

        Assert.Equal(1.4, scale, precision: 3);
    }

    [Fact]
    public void Resize_DraggingSidewaysOnly_StillResizes()
    {
        // Purely horizontal movement: the vertical axis is asking for no change at
        // all, so the fit lands short of what the width alone would give (+10%) -
        // but it must still move, rather than one axis being ignored.
        var scale = ScaleDrag.Resize(1.0, Width, Height, dragX: 64.4, dragY: 0);

        Assert.InRange(scale, 1.01, 1.1);
    }

    [Fact]
    public void Resize_OppositeDirections_LargelyCancel()
    {
        // Out on one axis, in on the other: the fit is whichever pull is stronger,
        // and on this panel that's the wide one.
        var scale = ScaleDrag.Resize(1.0, Width, Height, dragX: 64.4, dragY: -64.4);

        Assert.InRange(scale, 1.0, 1.05);
    }

    [Fact]
    public void Resize_PastTheTopOfTheBand_StopsAtTheMaximum()
        => Assert.Equal(LayoutGuard.MaxScale, ScaleDrag.Resize(1.75, Width, Height, 5000, 5000));

    [Fact]
    public void Resize_PastTheBottomOfTheBand_StopsAtTheMinimum()
        => Assert.Equal(LayoutGuard.MinScale, ScaleDrag.Resize(1.0, Width, Height, -5000, -5000));

    [Fact]
    public void Resize_RunPastTheEndAndBack_LandsWhereItStarted()
    {
        // The reason the drag is measured from a fixed origin rather than
        // accumulated: overshooting the top of the band must not bank movement that
        // then has to be unwound before the widget responds again.
        var overshot = ScaleDrag.Resize(1.5, Width, Height, 5000, 5000);
        var backAgain = ScaleDrag.Resize(1.5, Width, Height, 0, 0);

        Assert.Equal(LayoutGuard.MaxScale, overshot);
        Assert.Equal(1.5, backAgain);
    }

    [Fact]
    public void Resize_SmallMovement_LandsOnAWholePercent()
    {
        var scale = ScaleDrag.Resize(1.0, Width, Height, dragX: 7.3, dragY: 2.9);

        Assert.Equal(scale, Math.Round(scale, 2), precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(double.NaN)]
    public void Resize_ContentNotLaidOutYet_KeepsTheScale(double width)
        => Assert.Equal(1.25, ScaleDrag.Resize(1.25, width, Height, 100, 100));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Resize_NonFiniteDrag_KeepsTheScale(double drag)
        => Assert.Equal(1.25, ScaleDrag.Resize(1.25, Width, Height, drag, 0));

    [Fact]
    public void Resize_StartingOutsideTheBand_ComesBackInsideIt()
    {
        // A scale read off a window that somehow holds a value the band doesn't
        // allow shouldn't let the drag continue from there.
        Assert.Equal(LayoutGuard.MaxScale, ScaleDrag.Resize(4.0, Width, Height, 0, 0));
        Assert.Equal(LayoutGuard.MinScale, ScaleDrag.Resize(0.1, Width, Height, 0, 0));
    }
}
