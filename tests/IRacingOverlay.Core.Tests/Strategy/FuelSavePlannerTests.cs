using IRacingOverlay.Core.Fuel;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Tests.Strategy;

public class FuelSavePlannerTests
{
    [Fact]
    public void ShortOnFuelWithAKnownSaveCost_PricesBothWaysOut()
    {
        // 2.5 L/lap now, 2.1 needed to make it: saving 0.4 L/lap at 2 s/L costs
        // 0.8s a lap, over 20 laps, against a 29s stop.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.True(plan.HasPlan);
        Assert.Equal(0.8, plan.CostPerLapSeconds, precision: 6);
        Assert.Equal(20, plan.LapsRemaining);
        Assert.Equal(16, plan.TotalCostSeconds, precision: 6);
        Assert.Equal(29, plan.PitLossSeconds);
    }

    [Fact]
    public void EnoughFuelAlready_HasNoPlan()
    {
        // Nothing to decide - the strip is absent rather than pricing a save
        // nobody needs to make.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 20, willFinish: true),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.False(plan.HasPlan);
    }

    [Fact]
    public void SaveCostNotLearnedYet_HasNoPlan()
    {
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            SaveCostEstimate.None,
            pitLossSeconds: 29);

        Assert.False(plan.HasPlan);
    }

    [Fact]
    public void NoPitLossLearnedYet_StillPricesSaving()
    {
        // Half an answer is still an answer: what saving costs is measured off the
        // driver's own laps and doesn't wait on anyone else stopping.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: null);

        Assert.True(plan.HasPlan);
        Assert.Null(plan.PitLossSeconds);
    }

    [Fact]
    public void TargetFarBelowAnyLapDriven_HasNoPlan()
    {
        // Laps have been driven between 2.0 and 2.5 L. A target of 1.4 is more than
        // that whole range below the leanest of them, so the slope would be
        // describing driving nobody has done - a measured number turning into a
        // modelled one.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 1.4, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.False(plan.HasPlan);
    }

    [Fact]
    public void TargetJustBeyondTheObservedRange_IsStillPriced()
    {
        // One observed range past the leanest lap is the limit, and it is inclusive:
        // 2.0 - 0.5 = 1.5 is the furthest the fit is read.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 1.5, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.True(plan.HasPlan);
    }

    [Fact]
    public void TargetAboveCurrentBurn_HasNoPlan()
    {
        // Not a save at all. Can happen for a lap or two after a splash while the
        // burn average still carries the old stint.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.8, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.False(plan.HasPlan);
    }

    [Fact]
    public void NoRaceLengthKnown_HasNoPlan()
    {
        var plan = FuelSavePlanner.Compute(
            FuelStrategy.Unknown,
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.False(plan.HasPlan);
    }

    [Fact]
    public void PartLapRemaining_PricesTheWholeLapsOnly()
    {
        // The lap count is already rounded up to whole laps upstream; this only
        // guards against a fractional value reaching the multiplication.
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 4.7, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 29);

        Assert.Equal(4, plan.LapsRemaining);
        Assert.Equal(3.2, plan.TotalCostSeconds, precision: 6);
    }

    [Fact]
    public void ZeroPitLoss_IsTreatedAsUnknown()
    {
        // The tracker reports null, not zero, before it has learned - but a zero
        // arriving from anywhere else must not read as "stopping is free".
        var plan = FuelSavePlanner.Compute(
            Strategy(saveTarget: 2.1, laps: 20, willFinish: false),
            averageLitersPerLap: 2.5,
            Cost(secondsPerLiter: 2.0, lowest: 2.0, spread: 0.5),
            pitLossSeconds: 0);

        Assert.True(plan.HasPlan);
        Assert.Null(plan.PitLossSeconds);
    }

    private static FuelStrategy Strategy(double saveTarget, double laps, bool willFinish) =>
        new(
            RaceLapsRemaining: laps,
            FuelToFinishLiters: 50,
            MarginLaps: willFinish ? 1 : -1,
            FuelToAddLiters: 10,
            SaveTargetLitersPerLap: saveTarget,
            WillFinish: willFinish);

    private static SaveCostEstimate Cost(double secondsPerLiter, double lowest, double spread) =>
        new(secondsPerLiter, lowest, spread);
}
