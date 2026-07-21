using IRacingOverlay.Core.Session;

namespace IRacingOverlay.Core.Tests.Session;

public class TrackSectorsTests
{
    private static readonly double[] ThreeSectors = [0.0, 0.4, 0.72];

    [Theory]
    [InlineData(0.0, 1)]
    [InlineData(0.39, 1)]
    [InlineData(0.4, 2)]
    [InlineData(0.71, 2)]
    [InlineData(0.72, 3)]
    [InlineData(0.99, 3)]
    public void MapsPositionToItsSector(double pct, int expected) =>
        Assert.Equal(expected, TrackSectors.SectorAt(pct, ThreeSectors));

    [Fact]
    public void WrapsProjectedPositionPastTheLine()
    {
        // A meeting point of 1.35 laps is 0.35 into the next lap - sector 1.
        Assert.Equal(1, TrackSectors.SectorAt(1.35, ThreeSectors));

        // 2.5 laps -> 0.5 -> sector 2.
        Assert.Equal(2, TrackSectors.SectorAt(2.5, ThreeSectors));
    }

    [Fact]
    public void NoBoundaries_ReturnsNull()
    {
        Assert.Null(TrackSectors.SectorAt(0.5, null));
        Assert.Null(TrackSectors.SectorAt(0.5, []));
    }

    [Fact]
    public void NonFinitePosition_ReturnsNull()
    {
        Assert.Null(TrackSectors.SectorAt(double.NaN, ThreeSectors));
        Assert.Null(TrackSectors.SectorAt(double.PositiveInfinity, ThreeSectors));
    }

    [Fact]
    public void SingleSector_IsAlwaysSectorOne()
    {
        Assert.Equal(1, TrackSectors.SectorAt(0.0, [0.0]));
        Assert.Equal(1, TrackSectors.SectorAt(0.9, [0.0]));
    }
}
