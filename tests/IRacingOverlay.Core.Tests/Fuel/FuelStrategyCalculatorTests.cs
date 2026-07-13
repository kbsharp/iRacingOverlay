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
