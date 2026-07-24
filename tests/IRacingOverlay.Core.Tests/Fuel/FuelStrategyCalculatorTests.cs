using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Tests.Fuel;

public class FuelStrategyCalculatorTests
{
    [Fact]
    public void Compute_WithoutAverageBurn_ReturnsUnknown()
    {
        var strategy = FuelStrategyCalculator.Compute(
            currentFuelLiters: 40, averageLitersPerLap: null, raceLapsRemaining: 10);

        Assert.Equal(FuelStrategy.Unknown, strategy);
    }

    [Fact]
    public void Compute_WithoutRaceLength_ReturnsUnknown()
    {
        var strategy = FuelStrategyCalculator.Compute(
            currentFuelLiters: 40, averageLitersPerLap: 2.5, raceLapsRemaining: null);

        Assert.Equal(FuelStrategy.Unknown, strategy);
    }

    [Fact]
    public void Compute_FuelToFinishIsBurnTimesLaps()
    {
        var strategy = FuelStrategyCalculator.Compute(
            currentFuelLiters: 40, averageLitersPerLap: 2.5, raceLapsRemaining: 10);

        Assert.Equal(25.0, strategy.FuelToFinishLiters!.Value, 3);
    }

    [Fact]
    public void Compute_WithSurplus_ReportsPositiveMarginAndWillFinish()
    {
        // 40 L, 2.5 L/lap, 10 laps to go: needs 25 L, 15 L (6 laps) spare.
        var strategy = FuelStrategyCalculator.Compute(40, 2.5, 10);

        Assert.True(strategy.WillFinish);
        Assert.Equal(6.0, strategy.MarginLaps!.Value, 3);
        Assert.Equal(0.0, strategy.FuelToAddLiters!.Value, 3);
    }

    [Fact]
    public void Compute_WithDeficit_ReportsNegativeMarginAndWontFinish()
    {
        // 20 L, 2.5 L/lap, 10 laps: needs 25 L, 5 L (2 laps) short.
        var strategy = FuelStrategyCalculator.Compute(20, 2.5, 10);

        Assert.False(strategy.WillFinish);
        Assert.Equal(-2.0, strategy.MarginLaps!.Value, 3);
    }

    [Fact]
    public void Compute_WhenShort_FuelToAddCoversDeficitPlusSafetyBuffer()
    {
        // Needs 25 L, has 20 L, half-lap buffer = 1.25 L -> add 6.25 L.
        var strategy = FuelStrategyCalculator.Compute(
            20, 2.5, 10, safetyMarginLaps: 0.5);

        Assert.Equal(6.25, strategy.FuelToAddLiters!.Value, 3);
    }

    [Fact]
    public void Compute_SaveTargetIsFuelSpreadOverRemainingLaps()
    {
        // 20 L over 10 laps -> must average 2.0 L/lap to make it.
        var strategy = FuelStrategyCalculator.Compute(20, 2.5, 10);

        Assert.Equal(2.0, strategy.SaveTargetLitersPerLap!.Value, 3);
    }

    [Fact]
    public void Compute_ZeroLapsRemaining_FinishesWithNoSaveTarget()
    {
        var strategy = FuelStrategyCalculator.Compute(5, 2.5, 0);

        Assert.True(strategy.WillFinish);
        Assert.Equal(0.0, strategy.FuelToFinishLiters!.Value, 3);
        Assert.Null(strategy.SaveTargetLitersPerLap);
    }

    [Fact]
    public void Compute_WithoutCapacity_LeavesAddUncappedAndNoExtraStops()
    {
        // 5 L, 3 L/lap, 40 laps: needs 120 L + 1.5 L buffer -> add 116.5 L.
        // Capacity unknown (0) so the figure isn't capped - old behaviour.
        var strategy = FuelStrategyCalculator.Compute(
            5, 3, 40, safetyMarginLaps: 0.5, tankCapacityLiters: 0);

        Assert.Equal(116.5, strategy.FuelToAddLiters!.Value, 3);
        Assert.Equal(0, strategy.AdditionalStops);
    }

    [Fact]
    public void Compute_OneStopFits_DoesNotCapAddOrCountStops()
    {
        // Needs 25 L + 1.25 L buffer, has 20 L -> add 6.25 L, which fits in a
        // 60 L tank (40 L of space), so nothing is capped.
        var strategy = FuelStrategyCalculator.Compute(
            20, 2.5, 10, safetyMarginLaps: 0.5, tankCapacityLiters: 60);

        Assert.Equal(6.25, strategy.FuelToAddLiters!.Value, 3);
        Assert.Equal(0, strategy.AdditionalStops);
    }

    [Fact]
    public void Compute_TwoStopRace_CapsAddAtATankfulAndReportsOneMoreStop()
    {
        // 10 L, 3 L/lap, 30 laps: needs 90 L + 1.5 L buffer = 91.5 L, has 10 L, so
        // the deficit is 81.5 L. That won't fit in a 65 L tank, so the next stop
        // takes a full 65 L and one stop is still to come.
        var strategy = FuelStrategyCalculator.Compute(
            10, 3, 30, safetyMarginLaps: 0.5, tankCapacityLiters: 65);

        Assert.Equal(65.0, strategy.FuelToAddLiters!.Value, 3);
        Assert.Equal(1, strategy.AdditionalStops);
    }

    [Fact]
    public void Compute_ThreeStopRace_CountsBothStopsAfterTheNext()
    {
        // 10 L, 3 L/lap, 50 laps: deficit is 150 + 1.5 - 10 = 141.5 L. Over three
        // 50 L tankfuls (ceil(141.5 / 50) = 3), so two stops follow the next.
        var strategy = FuelStrategyCalculator.Compute(
            10, 3, 50, safetyMarginLaps: 0.5, tankCapacityLiters: 50);

        Assert.Equal(50.0, strategy.FuelToAddLiters!.Value, 3);
        Assert.Equal(2, strategy.AdditionalStops);
    }

    [Fact]
    public void Compute_AddNeverExceedsATankful()
    {
        // Whatever the deficit, the capped add can't be more than one tankful.
        var strategy = FuelStrategyCalculator.Compute(
            12, 4, 100, safetyMarginLaps: 0.5, tankCapacityLiters: 65);

        Assert.Equal(65.0, strategy.FuelToAddLiters!.Value, 3);
        Assert.True(strategy.AdditionalStops >= 1);
    }

    [Fact]
    public void EstimateRaceLapsRemaining_LapLimitedRace_UsesSessionLaps()
    {
        var laps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            sessionLapsRemaining: 12,
            sessionTimeRemainingSeconds: 999,
            averageLapTimeSeconds: 90);

        Assert.Equal(12.0, laps);
    }

    [Fact]
    public void EstimateRaceLapsRemaining_TimedRace_RoundsUpFromTimeAndLapTime()
    {
        // 5 minutes left, 90 s laps -> 3.33 laps -> rounded up to 4.
        var laps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            sessionLapsRemaining: FuelStrategyCalculator.UnlimitedLaps,
            sessionTimeRemainingSeconds: 300,
            averageLapTimeSeconds: 90);

        Assert.Equal(4.0, laps);
    }

    [Fact]
    public void EstimateRaceLapsRemaining_TimedRaceWithoutLapTime_ReturnsNull()
    {
        var laps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            sessionLapsRemaining: -1,
            sessionTimeRemainingSeconds: 300,
            averageLapTimeSeconds: null);

        Assert.Null(laps);
    }

    [Fact]
    public void EstimateRaceLapsRemaining_UnlimitedTime_ReturnsNull()
    {
        var laps = FuelStrategyCalculator.EstimateRaceLapsRemaining(
            sessionLapsRemaining: -1,
            sessionTimeRemainingSeconds: 604800,
            averageLapTimeSeconds: 90);

        Assert.Null(laps);
    }
}
