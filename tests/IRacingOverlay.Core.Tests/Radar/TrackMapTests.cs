using IRacingOverlay.Core.Radar;

namespace IRacingOverlay.Core.Tests.Radar;

public class TrackMapTests
{
    [Fact]
    public void NewMap_IsNotReady_AndZeroCoverage()
    {
        var map = new TrackMap(bucketCount: 100);

        Assert.False(map.IsReady);
        Assert.Equal(0.0, map.Coverage);
    }

    [Fact]
    public void Sample_FillingMostOfTheLap_MakesMapReady()
    {
        var map = new TrackMap(bucketCount: 100);

        // Fill 60 of 100 buckets (> 55% readiness threshold).
        for (var i = 0; i < 60; i++)
        {
            map.Sample((i + 0.5) / 100.0, headingRad: 0.0);
        }

        Assert.True(map.Coverage >= 0.6);
        Assert.True(map.IsReady);
    }

    [Fact]
    public void Sample_SameBucketTwice_DoesNotDoubleCountCoverage()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.005, 1.0);
        map.Sample(0.005, 2.0); // same bucket, updated heading

        Assert.Equal(0.01, map.Coverage, precision: 6);
        Assert.Equal(2.0, map.HeadingAt(0.005), precision: 6);
    }

    [Fact]
    public void Sample_FillsBucketsSkippedBetweenConsecutiveSamples()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.005, 0.5); // bucket 0
        map.Sample(0.045, 0.5); // bucket 4 - buckets 1,2,3 driven through are filled too

        Assert.Equal(0.05, map.Coverage, precision: 6); // 5 buckets
    }

    [Fact]
    public void Sample_FillsSkippedBucketsAcrossStartFinishLine()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.985, 1.0); // bucket 98
        map.Sample(0.015, 1.0); // bucket 1 - wraps, filling 99 and 0 between

        Assert.Equal(0.04, map.Coverage, precision: 6); // buckets 98, 99, 0, 1
    }

    [Fact]
    public void Sample_LargeForwardJump_FillsOnlyCurrentBucket()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.005, 0.5); // bucket 0
        map.Sample(0.505, 0.5); // bucket 50 - a teleport/reset, not driven through

        Assert.Equal(0.02, map.Coverage, precision: 6); // only buckets 0 and 50
    }

    [Fact]
    public void Sample_IgnoresNonFiniteInputs()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(double.NaN, 1.0);
        map.Sample(0.5, double.PositiveInfinity);

        Assert.Equal(0.0, map.Coverage);
    }

    [Fact]
    public void HeadingAt_UnfilledBucket_UsesNearestFilled()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.105, headingRad: 1.23); // bucket 10

        // A nearby unfilled position resolves to the closest learned heading.
        Assert.Equal(1.23, map.HeadingAt(0.135), precision: 6); // bucket 13
    }

    [Fact]
    public void HeadingAt_WrapsAcrossTheStartFinishLine()
    {
        var map = new TrackMap(bucketCount: 100);

        map.Sample(0.005, headingRad: 0.7); // bucket 0

        // Just before the line (bucket 99) should find bucket 0 by wrapping.
        Assert.Equal(0.7, map.HeadingAt(0.995), precision: 6);
    }
}
