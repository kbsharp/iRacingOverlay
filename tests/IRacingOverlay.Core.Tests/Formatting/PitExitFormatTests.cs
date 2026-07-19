using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Tests.Formatting;

public class PitExitFormatTests
{
    [Fact]
    public void NoProjection_ShowsNothingToDecode()
    {
        var none = PitExitProjection.None;

        Assert.Equal(TelemetryFormat.Placeholder, PitExitFormat.Position(none));
        Assert.Equal(string.Empty, PitExitFormat.PositionsLost(none));
        Assert.Equal(string.Empty, PitExitFormat.Cost(none));
        Assert.Equal(string.Empty, PitExitFormat.Neighbours(none));
    }

    [Fact]
    public void ReadsAsASentenceWithUnitsAndReferents()
    {
        var projection = Projection(classPosition: 4, lost: 2, ahead: ("12", 5.0), behind: ("7", 8.0));

        Assert.Equal("P4", PitExitFormat.Position(projection));
        Assert.Equal("▼2", PitExitFormat.PositionsLost(projection));
        Assert.Equal("costs 29s", PitExitFormat.Cost(projection));
        Assert.Equal("5.0s behind #12 · 8.0s clear of #7", PitExitFormat.Neighbours(projection));
    }

    [Fact]
    public void CostingNoPositions_ShowsNoArrow()
    {
        // "▼0" is a figure to read and then dismiss; the absence says it faster.
        var projection = Projection(classPosition: 2, lost: 0, ahead: ("12", 5.0), behind: ("7", 8.0));

        Assert.Equal(string.Empty, PitExitFormat.PositionsLost(projection));
    }

    [Fact]
    public void RejoiningAtTheFrontOfTheClass_SaysSo()
    {
        var projection = Projection(classPosition: 1, lost: 0, ahead: null, behind: ("7", 8.0));

        Assert.Equal("still leads the class · 8.0s clear of #7", PitExitFormat.Neighbours(projection));
    }

    [Fact]
    public void RejoiningLast_OmitsTheCarBehind()
    {
        var projection = Projection(classPosition: 6, lost: 1, ahead: ("12", 5.0), behind: null);

        Assert.Equal("5.0s behind #12", PitExitFormat.Neighbours(projection));
    }

    [Fact]
    public void NobodyEitherSide_SaysClearTrack()
    {
        var projection = Projection(classPosition: 3, lost: 0, ahead: null, behind: null);

        Assert.Equal("clear track", PitExitFormat.Neighbours(projection));
    }

    [Fact]
    public void GapsCarryOneDecimal_TheCostIsWhole()
    {
        // Tenths matter for a gap you're about to race; a learned median pit loss
        // quoted to a tenth would claim a precision it does not have.
        var projection = Projection(
            classPosition: 4, lost: 1, ahead: ("12", 5.04), behind: ("7", 8.26), pitLoss: 28.7);

        Assert.Equal("5.0s behind #12 · 8.3s clear of #7", PitExitFormat.Neighbours(projection));
        Assert.Equal("costs 29s", PitExitFormat.Cost(projection));
    }

    private static PitExitProjection Projection(
        int classPosition,
        int lost,
        (string Number, double Gap)? ahead,
        (string Number, double Gap)? behind,
        double pitLoss = 29.0) =>
        new(
            ClassPosition: classPosition,
            OverallPosition: classPosition,
            PositionsLost: lost,
            PitLossSeconds: pitLoss,
            CarAheadNumber: ahead?.Number ?? string.Empty,
            GapToCarAheadSeconds: ahead?.Gap,
            CarBehindNumber: behind?.Number ?? string.Empty,
            GapToCarBehindSeconds: behind?.Gap);
}
