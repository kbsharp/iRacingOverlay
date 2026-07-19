using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Tests.Radar;

public class RadarDangerTests
{
    private static RadarBlip At(double right, double forward)
        => new(1, right, forward, 0, "1", null);

    private static RadarBlip Unresolved(double forward)
        => new(1, 0, forward, 0, "1", null, LateralUnresolved: true);

    /// <summary>The bug this whole path exists for: a car the geometry couldn't place
    /// sits on the centreline, where the in-your-own-lane floor would drop it and leave
    /// the driver reading an empty mirror. The spotter's side carries it instead.</summary>
    [Fact]
    public void UnresolvedCarLightsTheSideTheSpotterReports()
    {
        var (left, right) = RadarDanger.Compute([Unresolved(0)], spotterLeft: true, spotterRight: false);

        Assert.True(left > 0.7);
        Assert.Equal(0, right);
    }

    [Fact]
    public void UnresolvedCarOnBothSidesLightsBoth()
    {
        var (left, right) = RadarDanger.Compute([Unresolved(0)], spotterLeft: true, spotterRight: true);

        Assert.True(left > 0.7);
        Assert.True(right > 0.7);
    }

    /// <summary>An unplaceable car is graded as a car in the next lane over - the same
    /// assumption used to place one when the side can be attributed, so resolving the
    /// ambiguity changes where the blip sits and not how hard the glow burns.</summary>
    [Fact]
    public void UnresolvedCarGlowsAsIfInTheNeighbouringLane()
    {
        var unresolved = RadarDanger.Compute([Unresolved(2)], spotterLeft: true).Left;
        var placed = RadarDanger.Compute([At(-RadarDanger.NeighbouringLaneMeters, 2)]).Left;

        Assert.Equal(placed, unresolved, precision: 6);
    }

    [Fact]
    public void UnresolvedCarWithoutASpotterCallStaysCalm()
    {
        var (left, right) = RadarDanger.Compute([Unresolved(0)]);

        Assert.Equal(0, left);
        Assert.Equal(0, right);
    }

    /// <summary>Side is unknown, but overlap isn't - so it still fades as the car
    /// drops back, rather than pegging red for the whole time it's within range.</summary>
    [Fact]
    public void UnresolvedCarFadesWithOverlap()
    {
        var level = RadarDanger.Compute([Unresolved(0)], spotterLeft: true).Left;
        var trailing = RadarDanger.Compute([Unresolved(4)], spotterLeft: true).Left;
        var gone = RadarDanger.Compute(
            [Unresolved(RadarDanger.OverlapRangeMeters)], spotterLeft: true).Left;

        Assert.True(level > trailing);
        Assert.True(trailing > 0);
        Assert.Equal(0, gone);
    }

    [Fact]
    public void EmptyFieldIsCalm()
    {
        var (left, right) = RadarDanger.Compute([]);

        Assert.Equal(0, left);
        Assert.Equal(0, right);
    }

    [Fact]
    public void CarOnYourDoorPegsThatSide()
    {
        var (left, right) = RadarDanger.Compute([At(-RadarDanger.MinLateralMeters, 0)]);

        Assert.True(left > 0.99);
        Assert.Equal(0, right);
    }

    [Fact]
    public void CarToTheRightOnlyLightsTheRight()
    {
        var (left, right) = RadarDanger.Compute([At(3, 1)]);

        Assert.Equal(0, left);
        Assert.True(right > 0);
    }

    /// <summary>The regression that the first pass got wrong: a train of cars queued
    /// nose-to-tail on the racing line is not a side-by-side situation, and lit both
    /// glows solid red for the whole lap.</summary>
    [Fact]
    public void CarsQueuedInYourOwnLaneDoNotLightEitherSide()
    {
        var (left, right) = RadarDanger.Compute([At(0, 4), At(0, -4), At(0.5, 2)]);

        Assert.Equal(0, left);
        Assert.Equal(0, right);
    }

    [Theory]
    [InlineData(RadarDanger.LateralRangeMeters, 0)]
    [InlineData(3, RadarDanger.OverlapRangeMeters)]
    [InlineData(40, 40)]
    public void CarsOutsideTheDangerBoxAreIgnored(double right, double forward)
    {
        var (l, r) = RadarDanger.Compute([At(right, forward)]);

        Assert.Equal(0, l);
        Assert.Equal(0, r);
    }

    [Fact]
    public void IntensityFadesAsTheCarDrawsAway()
    {
        var close = RadarDanger.Compute([At(2, 0)]).Right;
        var wide = RadarDanger.Compute([At(7, 0)]).Right;

        Assert.True(close > wide);
        Assert.True(wide > 0);
    }

    [Fact]
    public void ClosestCarOnASideWins()
    {
        var (_, right) = RadarDanger.Compute([At(7, 0), At(2, 0), At(5, 0)]);

        Assert.Equal(RadarDanger.Compute([At(2, 0)]).Right, right, 6);
    }
}
