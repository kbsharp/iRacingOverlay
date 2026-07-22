using IRacingOverlay.Core.Map;
using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Tests.Map;

public class TrackOutlineTests
{
    private const double CircleLength = 1000.0;

    [Fact]
    public void Build_MapNotReady_ReturnsNull()
    {
        var map = new TrackMap(bucketCount: 100);
        map.Sample(0.01, 0.0);

        Assert.Null(TrackOutline.Build(map, CircleLength));
    }

    /// <summary>The radar is happy at 55% because it only ever looks at the stretch
    /// around the player, which is always learned. The outline draws the whole lap,
    /// and an unlearned bucket comes out as a straight line through track nobody has
    /// driven yet - a wrong shape the driver can't see is wrong.</summary>
    [Fact]
    public void Build_MapReadyForTheRadarButHalfTheLapUnseen_StillDrawsNothing()
    {
        var buckets = 100;
        var map = new TrackMap(buckets);
        for (var i = 0; i < 60; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, (2 * Math.PI * pct) + (Math.PI / 2));
        }

        Assert.True(map.IsReady);
        Assert.Null(TrackOutline.Build(map, CircleLength));
    }

    [Fact]
    public void Build_UnknownTrackLength_ReturnsNull()
        => Assert.Null(TrackOutline.Build(CircleMap(), trackLengthMeters: 0));

    /// <summary>No real circuit is a straight line, but a lap of samples taken with a
    /// stuck heading is: the walk goes out in one direction and the closure correction
    /// folds it back onto itself, leaving nothing to draw. Better than drawing a
    /// shape the driver would read as their track.</summary>
    [Fact]
    public void Build_EveryHeadingIdentical_DrawsNothingRatherThanAFalseShape()
    {
        var buckets = 360;
        var map = new TrackMap(buckets);
        for (var i = 0; i < buckets; i++)
        {
            map.Sample((i + 0.5) / buckets, headingRad: 0.0);
        }

        Assert.Null(TrackOutline.Build(map, CircleLength));
    }

    [Fact]
    public void Build_CircularTrack_TracesACircle()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        Assert.Equal(720, outline.Points.Count);

        // Normalised into the unit box, a circle has its centre at (0.5, 0.5) and a
        // radius of 0.5 at every point around it.
        foreach (var point in outline.Points)
        {
            var radius = Math.Sqrt(
                ((point.X - 0.5) * (point.X - 0.5)) + ((point.Y - 0.5) * (point.Y - 0.5)));
            Assert.Equal(0.5, radius, precision: 2);
        }
    }

    [Fact]
    public void Build_NormalizesIntoTheUnitBox()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        Assert.All(outline.Points, p =>
        {
            Assert.InRange(p.X, 0.0, 1.0);
            Assert.InRange(p.Y, 0.0, 1.0);
        });
    }

    /// <summary>The corrected walk must come back to the line: an open loop draws a
    /// circuit with a visible gap where the start/finish straight should be.</summary>
    [Fact]
    public void Build_ClosesTheLoop_EvenWhenTheHeadingsDoNotQuiteAddUp()
    {
        // A circle whose headings are 3% short of a full turn - the walk ends well
        // adrift of where it started, exactly as a real learned lap does.
        var buckets = 720;
        var map = new TrackMap(buckets);
        for (var i = 0; i < buckets; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, (2 * Math.PI * pct * 0.97) + (Math.PI / 2));
        }

        var outline = TrackOutline.Build(map, CircleLength);

        Assert.NotNull(outline);
        var first = outline.Points[0];
        var last = outline.Points[^1];
        var gap = Math.Sqrt(
            ((last.X - first.X) * (last.X - first.X)) + ((last.Y - first.Y) * (last.Y - first.Y)));

        // One bucket's worth of track, not a visible seam.
        Assert.True(gap < 0.02, $"loop left a {gap:0.000} gap at the line");
    }

    /// <summary>Uniform scaling, not a stretch to fill: a long thin circuit must come
    /// out long and thin, with the spare space either side of it.</summary>
    [Fact]
    public void Build_LongThinTrack_KeepsItsAspectRatio()
    {
        var outline = TrackOutline.Build(OvalMap(), CircleLength);

        Assert.NotNull(outline);

        double minX = 1, maxX = 0, minY = 1, maxY = 0;
        foreach (var point in outline.Points)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        // The oval is a quarter-lap straight each side of two half-lap turns, so it
        // measures (0.25 + 2r) by 2r with r = 0.25/pi: about 2.57 times as long as
        // it is wide. The long axis fills the box, the short one keeps that ratio
        // and sits centred - the circuit, not a squashed version of it.
        var expectedHeight = (2 * 0.25 / Math.PI) / (0.25 + (2 * 0.25 / Math.PI));
        Assert.Equal(1.0, maxX - minX, precision: 2);
        Assert.Equal(expectedHeight, maxY - minY, precision: 2);
        Assert.Equal(0.5, (minY + maxY) / 2, precision: 2);
    }

    [Fact]
    public void At_StartOfLap_IsTheFirstPoint()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        Assert.Equal(outline.Points[0].X, outline.At(0.0).X, precision: 9);
        Assert.Equal(outline.Points[0].Y, outline.At(0.0).Y, precision: 9);
    }

    [Fact]
    public void At_BetweenBuckets_InterpolatesRatherThanSnapping()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        var a = outline.Points[0];
        var b = outline.Points[1];
        var mid = outline.At(0.5 / 720);

        Assert.Equal((a.X + b.X) / 2, mid.X, precision: 9);
        Assert.Equal((a.Y + b.Y) / 2, mid.Y, precision: 9);
    }

    [Fact]
    public void At_WrapsAtTheLine()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        Assert.Equal(outline.At(0.0).X, outline.At(1.0).X, precision: 9);
        Assert.Equal(outline.At(0.25).X, outline.At(2.25).X, precision: 9);
        Assert.Equal(outline.At(0.25).Y, outline.At(-1.75).Y, precision: 9);
    }

    [Fact]
    public void At_NonFiniteInput_FallsBackToTheLine()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);
        Assert.Equal(outline.Points[0], outline.At(double.NaN));
    }

    [Fact]
    public void Build_PartialMap_ReportsTheCoverageItWasDrawnFrom()
    {
        var buckets = 100;
        var map = new TrackMap(buckets);
        for (var i = 0; i < 92; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, (2 * Math.PI * pct) + (Math.PI / 2));
        }

        var outline = TrackOutline.Build(map, CircleLength);

        Assert.NotNull(outline);
        Assert.Equal(0.92, outline.Coverage, precision: 6);
    }

    /// <summary>Anticlockwise in the world must come out clockwise on screen, because
    /// the Y axis flips: north is up, and a driver reads the map that way round.</summary>
    [Fact]
    public void Build_FlipsYIntoScreenSpace()
    {
        var outline = TrackOutline.Build(CircleMap(), CircleLength);

        Assert.NotNull(outline);

        // A quarter lap round an anticlockwise circle starting at the east point
        // heads north - which is up the screen, so Y must have decreased.
        Assert.True(outline.At(0.25).Y < outline.At(0.0).Y);
    }

    /// <summary>Anticlockwise unit circle: at lap fraction p the car sits at angle
    /// 2*pi*p and points a quarter turn further round.</summary>
    private static TrackMap CircleMap(int buckets = 720)
    {
        var map = new TrackMap(buckets);
        for (var i = 0; i < buckets; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, (2 * Math.PI * pct) + (Math.PI / 2));
        }

        return map;
    }

    /// <summary>A rounded oval: a quarter-lap straight, a quarter-lap 180, and the
    /// same again - the simplest circuit that is plainly longer than it is wide.</summary>
    private static TrackMap OvalMap(int buckets = 720)
    {
        // Half the lap is the two 180-degree turns, half is the two straights.
        var map = new TrackMap(buckets);
        var heading = 0.0;

        for (var i = 0; i < buckets; i++)
        {
            var pct = (i + 0.5) / buckets;
            map.Sample(pct, heading);

            // Quarters 2 and 4 are the turns; each sweeps a full half-circle.
            var inTurn = (pct >= 0.25 && pct < 0.5) || pct >= 0.75;
            if (inTurn)
            {
                heading += Math.PI / (buckets / 4.0);
            }
        }

        return map;
    }
}
