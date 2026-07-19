using IRacingOverlay.Core.Rating;

namespace IRacingOverlay.Core.Tests.Rating;

public class CpiHistoryTests
{
    [Fact]
    public void EmptyHistoryHasNoBaseline()
    {
        Assert.Null(CpiHistory.Empty.AverageCpi);
    }

    [Fact]
    public void BaselineStaysHiddenUntilEnoughCornersAreBanked()
    {
        var history = CpiHistory.Empty.Add(CpiHistory.MinimumCorners - 1, 2);

        Assert.Null(history.AverageCpi);
    }

    [Fact]
    public void BaselineIsCornersOverIncidentsOnceThereIsEnoughEvidence()
    {
        var history = CpiHistory.Empty.Add(600, 4);

        Assert.Equal(150, history.AverageCpi);
    }

    /// <summary>
    /// A driver who hasn't put a wheel wrong has no upper bound on their average,
    /// so every future session would score "worse" against it. Showing no
    /// direction is the honest answer - see <see cref="CpiHistory.AverageCpi"/>.
    /// </summary>
    [Fact]
    public void SpotlessHistoryReportsNoBaselineRatherThanInfinity()
    {
        var history = CpiHistory.Empty.Add(1000, 0);

        Assert.Null(history.AverageCpi);
    }

    [Fact]
    public void SessionsAccumulateWhileTheWindowHasRoom()
    {
        var history = CpiHistory.Empty.Add(400, 2).Add(600, 3);

        Assert.Equal(1000, history.Corners);
        Assert.Equal(5, history.IncidentPoints);
    }

    [Fact]
    public void WindowNeverGrowsBeyondItsCornerBudget()
    {
        var history = CpiHistory.Empty;

        for (var i = 0; i < 20; i++)
        {
            history = history.Add(500, 2);
        }

        Assert.Equal(CpiHistory.WindowCorners, history.Corners, precision: 6);
    }

    /// <summary>
    /// The point of the rolling window: a driver who cleans up their act should
    /// see the baseline follow them, not be anchored to a bad month forever.
    ///
    /// It follows <i>gradually</i> - eight tidy sessions move a CPI-50 baseline
    /// to the mid-200s, not to the 500 those sessions were run at - because the
    /// old evidence is scaled out rather than dropped. That is the intended
    /// feel and it matches how slowly real Safety Rating moves; a baseline that
    /// snapped to the last session would just be the last session.
    /// </summary>
    [Fact]
    public void RecentSessionsDisplaceOlderOnes()
    {
        var messy = CpiHistory.Empty;
        for (var i = 0; i < 8; i++)
        {
            messy = messy.Add(500, 10);      // CPI 50
        }

        var reformed = messy;
        for (var i = 0; i < 8; i++)
        {
            reformed = reformed.Add(500, 1); // CPI 500
        }

        Assert.Equal(50, messy.AverageCpi!.Value, precision: 6);
        Assert.True(reformed.AverageCpi > 200, $"baseline stuck at {reformed.AverageCpi}");
    }

    /// <summary>
    /// A 24-hour enduro is longer than the whole window. It must not be able to
    /// bank more corners than the budget, and truncating it has to preserve its
    /// CPI rather than keeping all the incidents against a fraction of the laps.
    /// </summary>
    [Fact]
    public void OversizedSessionIsTruncatedWithoutDistortingItsRate()
    {
        var history = CpiHistory.Empty.Add(20000, 100);   // CPI 200

        Assert.Equal(CpiHistory.WindowCorners, history.Corners, precision: 6);
        Assert.Equal(200, history.AverageCpi!.Value, precision: 6);
    }

    [Fact]
    public void EmptySessionIsIgnored()
    {
        var history = CpiHistory.Empty.Add(600, 4);

        Assert.Equal(history, history.Add(0, 3));
    }
}
