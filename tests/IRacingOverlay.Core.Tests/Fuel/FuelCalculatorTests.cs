using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Tests.Fuel;

public class FuelCalculatorTests
{
    [Fact]
    public void Update_BeforeAnyCompletedLap_ReturnsEmptyEstimate()
    {
        var calculator = new FuelCalculator();

        var estimate = calculator.Update(lap: 1, fuelLevelLiters: 50f);

        Assert.Equal(FuelEstimate.Empty, estimate);
    }

    [Fact]
    public void Update_FramesWithinTheSameLap_RecordNothing()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        calculator.Update(1, 49f);
        var estimate = calculator.Update(1, 48f);

        Assert.Equal(0, estimate.LapsCounted);
    }

    [Fact]
    public void Update_AfterOneCompletedLap_ReportsThatLapsUsage()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        var estimate = calculator.Update(2, 47.5f);

        Assert.NotNull(estimate.AverageLitersPerLap);
        Assert.NotNull(estimate.LastLapLiters);
        Assert.Equal(2.5, estimate.AverageLitersPerLap.Value, 3);
        Assert.Equal(2.5, estimate.LastLapLiters.Value, 3);
        Assert.Equal(1, estimate.LapsCounted);
    }

    [Fact]
    public void Update_AveragesOnlyTheMostRecentWindowOfLaps()
    {
        var calculator = new FuelCalculator(windowSize: 2);

        calculator.Update(1, 50f);
        calculator.Update(2, 48f); // 2.0 L - should fall out of the window
        calculator.Update(3, 45f); // 3.0 L
        var estimate = calculator.Update(4, 41f); // 4.0 L

        Assert.NotNull(estimate.AverageLitersPerLap);
        Assert.Equal(3.5, estimate.AverageLitersPerLap.Value, 3);
        Assert.Equal(2, estimate.LapsCounted);
    }

    [Fact]
    public void Update_EstimatesLapsRemainingFromCurrentFuel()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        calculator.Update(2, 47.5f);
        var estimate = calculator.Update(2, 10f);

        Assert.NotNull(estimate.EstimatedLapsRemaining);
        Assert.Equal(4.0, estimate.EstimatedLapsRemaining.Value, 3);
    }

    [Fact]
    public void Update_RefuelMidLap_InvalidatesThatLapOnly()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        calculator.Update(1, 48f);
        calculator.Update(1, 60f);  // refuel in the pits
        var afterRefuel = calculator.Update(2, 59f);
        var nextLap = calculator.Update(3, 56.5f);

        Assert.Equal(0, afterRefuel.LapsCounted);
        Assert.Equal(1, nextLap.LapsCounted);
        Assert.NotNull(nextLap.AverageLitersPerLap);
        Assert.Equal(2.5, nextLap.AverageLitersPerLap.Value, 3);
    }

    [Fact]
    public void Update_SmallFuelReadingNoise_IsNotTreatedAsRefuel()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        calculator.Update(1, 49.85f);
        calculator.Update(1, 49.9f); // +0.05 L sensor noise
        var estimate = calculator.Update(2, 47.5f);

        Assert.Equal(1, estimate.LapsCounted);
        Assert.NotNull(estimate.AverageLitersPerLap);
        Assert.Equal(2.5, estimate.AverageLitersPerLap.Value, 3);
    }

    [Fact]
    public void Update_LapCounterGoingBackwards_RebaselinesAndKeepsHistory()
    {
        var calculator = new FuelCalculator();

        calculator.Update(5, 40f);
        calculator.Update(6, 38f);          // 2.0 L recorded
        var afterReset = calculator.Update(2, 45f); // tow / session restart
        var nextLap = calculator.Update(3, 43f);    // 2.0 L recorded

        Assert.Equal(1, afterReset.LapsCounted);
        Assert.Equal(2, nextLap.LapsCounted);
        Assert.NotNull(nextLap.AverageLitersPerLap);
        Assert.Equal(2.0, nextLap.AverageLitersPerLap.Value, 3);
    }

    [Fact]
    public void Update_LapCounterJumpingForwardByMoreThanOne_IsNotRecorded()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        var afterJump = calculator.Update(3, 45f);
        var nextLap = calculator.Update(4, 42.5f);

        Assert.Equal(0, afterJump.LapsCounted);
        Assert.Equal(1, nextLap.LapsCounted);
        Assert.NotNull(nextLap.AverageLitersPerLap);
        Assert.Equal(2.5, nextLap.AverageLitersPerLap.Value, 3);
    }

    [Fact]
    public void Update_ZeroFuelUsedOverALap_IsNotRecorded()
    {
        var calculator = new FuelCalculator();

        calculator.Update(1, 50f);
        var estimate = calculator.Update(2, 50f);

        Assert.Equal(0, estimate.LapsCounted);
    }

    [Fact]
    public void Reset_ClearsAllRecordedHistory()
    {
        var calculator = new FuelCalculator();
        calculator.Update(1, 50f);
        calculator.Update(2, 47.5f);

        calculator.Reset();
        var estimate = calculator.Update(3, 47f);

        Assert.Equal(FuelEstimate.Empty, estimate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WindowSizeBelowOne_Throws(int windowSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuelCalculator(windowSize));
    }
}
