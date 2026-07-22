using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Map;

/// <summary>A point on the drawn circuit, normalised into the unit box: X right,
/// Y <b>down</b> (screen convention, so a widget binds straight through).</summary>
public readonly record struct TrackPoint(double X, double Y);

/// <summary>
/// The circuit's drawable shape, walked out of the learned <see cref="TrackMap"/>.
///
/// The map stores a heading for every point around the lap; a heading plus a
/// distance is a direction and a step, so integrating one step per bucket traces
/// the outline of the track itself. That is the whole trick, and it means the
/// map needs <b>no track database</b>: it is drawn from the player's own driving,
/// so it is never missing a circuit and never stale after a resurface.
///
/// Two corrections make the trace presentable:
///
/// <list type="bullet">
/// <item><description><b>Closure.</b> Small heading errors accumulate over a lap,
/// so the raw walk ends up short of where it started - a circuit with a visible
/// gap at the line. The miss is spread backwards over the lap in proportion to
/// distance travelled (the surveyor's compass rule), which shuts the loop without
/// bending any one part of it noticeably.</description></item>
/// <item><description><b>Normalisation.</b> The result is scaled uniformly (never
/// stretched - a distorted circuit is worse than a small one) and centred into
/// the unit box, so the widget can size its canvas however it likes.</description></item>
/// </list>
///
/// Immutable once built: <see cref="Build"/> snapshots the map, so a caller can
/// keep drawing the same outline while the map behind it keeps learning.
/// </summary>
public sealed class TrackOutline
{
    /// <summary>
    /// How much of the track's own length the drawn shape has to span before it is
    /// believed. The most compact closed lap there is - a circle - is still about
    /// a third of its length across, so any real circuit clears this by a mile.
    /// What it catches is the walk that folds back onto itself (a lap of samples
    /// taken with a stuck heading): the closure correction leaves nothing but
    /// floating-point residue, which normalisation would otherwise magnify into a
    /// confident-looking shape drawn from nothing.
    /// </summary>
    private const double MinExtentFraction = 0.01;

    private readonly TrackPoint[] _points;

    private TrackOutline(TrackPoint[] points, double coverage)
    {
        _points = points;
        Coverage = coverage;
    }

    /// <summary>The outline, one point per track-map bucket, ordered by lap
    /// fraction from the start/finish line. The shape is closed: the last point
    /// joins back to the first.</summary>
    public IReadOnlyList<TrackPoint> Points => _points;

    /// <summary>The map coverage this outline was built from - how much of the lap
    /// had actually been driven. Below 1.0 the unlearned stretches are drawn from
    /// the nearest learned heading, so the shape is an approximation there.</summary>
    public double Coverage { get; }

    /// <summary>
    /// Traces the outline from a learned track map, or null when there is nothing
    /// honest to draw - an unready map, an unknown track length, or a walk that
    /// collapses to a point (a stationary car's worth of samples).
    /// </summary>
    public static TrackOutline? Build(TrackMap map, double trackLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!map.IsReady || trackLengthMeters <= 0)
        {
            return null;
        }

        var n = map.BucketCount;
        var stepMeters = trackLengthMeters / n;

        // Walk the lap. Each bucket contributes one step in its own direction,
        // sampled at the bucket's midpoint to match RadarGeometry's convention.
        var walk = new TrackPoint[n + 1];
        double x = 0, y = 0;
        for (var i = 0; i < n; i++)
        {
            walk[i] = new TrackPoint(x, y);
            var heading = map.HeadingAt((i + 0.5) / n);
            x += Math.Cos(heading) * stepMeters;
            y += Math.Sin(heading) * stepMeters;
        }

        walk[n] = new TrackPoint(x, y);

        // Close the loop: the walk should have ended where it began, so share the
        // miss out along the lap rather than leaving it all at the line.
        var points = new TrackPoint[n];
        for (var i = 0; i < n; i++)
        {
            var share = (double)i / n;
            points[i] = new TrackPoint(walk[i].X - (walk[n].X * share), walk[i].Y - (walk[n].Y * share));
        }

        return Normalize(points, map.Coverage, trackLengthMeters);
    }

    /// <summary>
    /// The point on the outline at a lap fraction, interpolated between the two
    /// buckets either side of it - so a car crosses the drawn line smoothly rather
    /// than snapping from bucket to bucket. Wraps, so 1.0 is 0.0.
    /// </summary>
    public TrackPoint At(double lapDistPct)
    {
        if (!double.IsFinite(lapDistPct))
        {
            return _points[0];
        }

        var n = _points.Length;
        var exact = (lapDistPct - Math.Floor(lapDistPct)) * n;
        var index = (int)exact;
        if (index >= n)
        {
            index = n - 1;
        }

        var next = (index + 1) % n;
        var t = exact - index;

        var a = _points[index];
        var b = _points[next];
        return new TrackPoint(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
    }

    /// <summary>
    /// Scale uniformly into the unit box and centre, flipping Y so the shape comes
    /// out in screen coordinates. Uniform because a circuit stretched to fill a
    /// square stops being the circuit the driver knows; the spare space either side
    /// of a long thin track is the honest cost of that.
    /// </summary>
    private static TrackOutline? Normalize(
        TrackPoint[] points, double coverage, double trackLengthMeters)
    {
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var point in points)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            {
                return null;
            }

            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        var width = maxX - minX;
        var height = maxY - minY;
        var extent = Math.Max(width, height);
        if (extent < trackLengthMeters * MinExtentFraction)
        {
            return null; // a lap that goes nowhere - nothing honest to draw
        }

        var scale = 1.0 / extent;
        var offsetX = (1.0 - (width * scale)) / 2;
        var offsetY = (1.0 - (height * scale)) / 2;

        var normalized = new TrackPoint[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            normalized[i] = new TrackPoint(
                ((points[i].X - minX) * scale) + offsetX,
                // World +Y is north; screen +Y is down.
                ((maxY - points[i].Y) * scale) + offsetY);
        }

        return new TrackOutline(normalized, coverage);
    }
}
