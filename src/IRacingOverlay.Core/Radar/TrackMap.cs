namespace IRacingOverlay.Core.Radar;

/// <summary>
/// The learned shape of the current track: the player's heading (radians, world
/// frame) sampled at every point around the lap, bucketed by <c>LapDistPct</c>.
/// Built up live from the player's own driving - iRacing gives us the player's
/// <c>Yaw</c> but no heading for other cars, so we record ours as we go and
/// reuse it to place everyone else (see <see cref="RadarGeometry"/>).
///
/// Headings are stored raw and only ever consumed via <see cref="Math.Cos"/>/
/// <see cref="Math.Sin"/> or angle differences, so no unwrapping is needed.
/// Callers should only feed samples while the player is driving forwards at
/// speed; a stationary or spun car would otherwise poison a bucket (it gets
/// overwritten on the next clean lap regardless).
/// </summary>
public sealed class TrackMap
{
    private const int DefaultBucketCount = 720; // 0.5 deg of lap per bucket

    private readonly double[] _headings;
    private readonly bool[] _filled;
    private int _filledCount;
    private int _lastBucket = -1;

    public TrackMap(int bucketCount = DefaultBucketCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bucketCount, 8);
        _headings = new double[bucketCount];
        _filled = new bool[bucketCount];
    }

    public int BucketCount => _headings.Length;

    /// <summary>Fraction of the lap whose shape has been learned, in [0, 1].</summary>
    public double Coverage => (double)_filledCount / _headings.Length;

    /// <summary>
    /// True once enough of the lap is mapped to place cars faithfully - roughly
    /// one clean lap. Below this the caller should fall back to the coarse
    /// spotter signal rather than draw a half-learned track.
    /// </summary>
    public bool IsReady => Coverage >= 0.55;

    /// <summary>
    /// Forget the learned shape. Called when the sim moves to a different track:
    /// the buckets are keyed by lap fraction, so last track's headings would keep
    /// being served for this one - a radar placing cars against a circuit nobody
    /// is driving, and a map drawing its outline.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_headings);
        Array.Clear(_filled);
        _filledCount = 0;
        _lastBucket = -1;
    }

    /// <summary>
    /// Record the player's heading at a point on the lap. Silently ignores
    /// out-of-range or non-finite inputs. Callers gate on the player actually
    /// moving; see the type remarks.
    ///
    /// At 15 Hz the player skips several buckets between samples, so the buckets
    /// passed since the last sample are filled with this heading too - that maps
    /// the whole lap in a single pass instead of a slow fill over several laps.
    /// A large forward jump (teleport, tow, or the very first sample) fills only
    /// the current bucket, so a reset can't smear a false line across the track.
    /// </summary>
    public void Sample(double lapDistPct, double headingRad)
    {
        if (!double.IsFinite(lapDistPct) || !double.IsFinite(headingRad))
        {
            return;
        }

        var bucket = BucketOf(lapDistPct);
        var n = _headings.Length;
        var gap = _lastBucket < 0 ? 0 : (bucket - _lastBucket + n) % n;

        if (gap > 0 && gap <= n / 20)
        {
            // Fill every bucket the player drove through since the last sample.
            for (var b = (_lastBucket + 1) % n; ; b = (b + 1) % n)
            {
                Fill(b, headingRad);
                if (b == bucket)
                {
                    break;
                }
            }
        }
        else
        {
            Fill(bucket, headingRad);
        }

        _lastBucket = bucket;
    }

    private void Fill(int bucket, double headingRad)
    {
        _headings[bucket] = headingRad;
        if (!_filled[bucket])
        {
            _filled[bucket] = true;
            _filledCount++;
        }
    }

    /// <summary>
    /// Heading at a lap fraction. Uses the nearest learned bucket when the exact
    /// one hasn't been seen yet; returns 0 only if nothing has been mapped at all
    /// (callers should have checked <see cref="IsReady"/> first).
    /// </summary>
    public double HeadingAt(double lapDistPct)
    {
        if (_filledCount == 0)
        {
            return 0.0;
        }

        var bucket = BucketOf(lapDistPct);
        if (_filled[bucket])
        {
            return _headings[bucket];
        }

        // Walk outward symmetrically to the closest learned bucket.
        var n = _headings.Length;
        for (var offset = 1; offset <= n / 2; offset++)
        {
            var ahead = (bucket + offset) % n;
            if (_filled[ahead])
            {
                return _headings[ahead];
            }

            var behind = (bucket - offset + n) % n;
            if (_filled[behind])
            {
                return _headings[behind];
            }
        }

        return 0.0;
    }

    private int BucketOf(double lapDistPct)
    {
        var pct = lapDistPct - Math.Floor(lapDistPct); // -> [0, 1)
        var bucket = (int)(pct * _headings.Length);
        // Guard the boundary where pct rounds up into the bucket count.
        return bucket >= _headings.Length ? _headings.Length - 1 : bucket;
    }
}
