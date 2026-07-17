using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Tests.Radar;

public class RadarGeometryTests
{
    private const double TrackLength = 1000.0; // metres, round numbers for readable asserts

    /// <summary>A fully-learned map whose heading at every point is given by <paramref name="heading"/>.</summary>
    private static TrackMap MapFrom(Func<double, double> heading, int buckets = 720)
    {
        var map = new TrackMap(buckets);
        for (var i = 0; i < buckets; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, heading(pct));
        }

        return map;
    }

    [Fact]
    public void Straight_CarAhead_SitsDirectlyAhead()
    {
        var map = MapFrom(_ => 0.0); // dead straight, constant heading

        // 10 m ahead on a 1000 m track = 0.01 of a lap.
        var (right, forward, angle) = RadarGeometry.RelativeTo(map, 0.5, 0.51, TrackLength);

        Assert.Equal(10.0, forward, precision: 3);
        Assert.Equal(0.0, right, precision: 3);
        Assert.Equal(0.0, angle, precision: 3);
    }

    [Fact]
    public void Straight_CarBehind_SitsDirectlyBehind()
    {
        var map = MapFrom(_ => 0.0);

        var (right, forward, angle) = RadarGeometry.RelativeTo(map, 0.5, 0.49, TrackLength);

        Assert.Equal(-10.0, forward, precision: 3);
        Assert.Equal(0.0, right, precision: 3);
        Assert.Equal(0.0, angle, precision: 3);
    }

    [Fact]
    public void Straight_DifferentReferenceHeading_StillPlacesCarAhead()
    {
        // The absolute heading must cancel: a track running at 1 rad still puts a
        // car ahead directly in front, not off at an angle.
        var map = MapFrom(_ => 1.0);

        var (right, forward, angle) = RadarGeometry.RelativeTo(map, 0.3, 0.31, TrackLength);

        Assert.Equal(10.0, forward, precision: 3);
        Assert.Equal(0.0, right, precision: 3);
        Assert.Equal(0.0, angle, precision: 3);
    }

    [Fact]
    public void LeftCorner_CarAhead_IsOffToTheLeftAndAngledLeft()
    {
        // Heading rises with lap position: a constant left-hand (anticlockwise)
        // curve. A car ahead should sit forward, to the player's left, nose
        // rotated left. Full lap = one full circle (heading sweeps 2*pi).
        var map = MapFrom(pct => 2 * Math.PI * pct);

        var (right, forward, angle) = RadarGeometry.RelativeTo(map, 0.0, 0.05, TrackLength);

        Assert.True(forward > 0, $"expected car ahead, forward={forward}");
        Assert.True(right < 0, $"expected car to the left, right={right}");
        // 0.05 of a lap round a full circle = 18 degrees of heading change.
        Assert.Equal(2 * Math.PI * 0.05, angle, precision: 3);
    }

    [Fact]
    public void RightCorner_CarAhead_IsOffToTheRightAndAngledRight()
    {
        // Heading falls with lap position: a right-hand (clockwise) curve.
        var map = MapFrom(pct => -2 * Math.PI * pct);

        var (right, forward, angle) = RadarGeometry.RelativeTo(map, 0.0, 0.05, TrackLength);

        Assert.True(forward > 0, $"expected car ahead, forward={forward}");
        Assert.True(right > 0, $"expected car to the right, right={right}");
        Assert.Equal(-2 * Math.PI * 0.05, angle, precision: 3);
    }

    [Fact]
    public void WrapsShortWayRoundTheStartFinishLine()
    {
        var map = MapFrom(_ => 0.0);

        // Player just before the line, car just after: 20 m ahead, not a lap behind.
        var (_, forward, _) = RadarGeometry.RelativeTo(map, 0.99, 0.01, TrackLength);

        Assert.Equal(20.0, forward, precision: 3);
    }
}
