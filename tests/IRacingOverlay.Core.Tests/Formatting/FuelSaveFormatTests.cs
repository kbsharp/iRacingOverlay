using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Tests.Formatting;

public class FuelSaveFormatTests
{
    [Fact]
    public void TotalCost_UnderTenSeconds_KeepsATenth()
    {
        var plan = new FuelSavePlan(0.4, 14, 5.6, 29);

        Assert.Equal("5.6s", FuelSaveFormat.TotalCost(plan));
    }

    [Fact]
    public void TotalCost_OverTenSeconds_RoundsToWholeSeconds()
    {
        // Quoting "34.2s" against a pit loss shown as "29s" would claim a precision
        // neither estimate has.
        var plan = new FuelSavePlan(1.2, 28, 34.2, 29);

        Assert.Equal("34s", FuelSaveFormat.TotalCost(plan));
    }

    [Fact]
    public void TotalCost_NoPlan_IsThePlaceholder()
    {
        Assert.Equal(TelemetryFormat.Placeholder, FuelSaveFormat.TotalCost(FuelSavePlan.None));
    }

    [Fact]
    public void Alternative_NamesWhatStoppingCostsInstead()
    {
        var plan = new FuelSavePlan(0.4, 14, 5.6, 29);

        Assert.Equal("vs 29s to pit", FuelSaveFormat.Alternative(plan));
    }

    [Fact]
    public void Alternative_WithoutALearnedPitLoss_IsEmpty()
    {
        var plan = new FuelSavePlan(0.4, 14, 5.6, null);

        Assert.Equal(string.Empty, FuelSaveFormat.Alternative(plan));
    }

    [Fact]
    public void Working_CarriesTheRateItsUnitAndTheLapsItRunsFor()
    {
        var plan = new FuelSavePlan(0.4, 14, 5.6, 29);

        Assert.Equal("0.4s/lap slower for 14 laps", FuelSaveFormat.Working(plan));
    }

    [Fact]
    public void Working_OnTheLastLap_ReadsAsOneLap()
    {
        var plan = new FuelSavePlan(0.4, 1, 0.4, 29);

        Assert.Equal("0.4s/lap slower for 1 lap", FuelSaveFormat.Working(plan));
    }

    [Fact]
    public void Working_NoPlan_IsEmpty()
    {
        Assert.Equal(string.Empty, FuelSaveFormat.Working(FuelSavePlan.None));
    }
}
