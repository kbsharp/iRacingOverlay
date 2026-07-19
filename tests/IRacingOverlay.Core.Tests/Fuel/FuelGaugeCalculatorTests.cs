using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Tests.Fuel;

public sealed class FuelGaugeCalculatorTests
{
    [Fact]
    public void HalfTank_FillsHalfTheBar()
    {
        var gauge = FuelGaugeCalculator.Compute(32.5, 65, null);

        Assert.True(gauge.HasGauge);
        Assert.Equal(0.5, gauge.FillFraction, 3);
        Assert.False(gauge.ShowTick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void NoCapacity_HidesTheGauge(double capacity)
    {
        // Better no bar than a bar drawn against a guessed scale - a gauge that
        // reads "half full" when it isn't is worse than no gauge at all.
        var gauge = FuelGaugeCalculator.Compute(30, capacity, 20);

        Assert.False(gauge.HasGauge);
    }

    [Fact]
    public void LevelAboveToFinish_ClearsTheTick()
    {
        var gauge = FuelGaugeCalculator.Compute(40, 65, 32);

        Assert.True(gauge.ShowTick);
        Assert.Equal(32d / 65d, gauge.TickFraction, 3);
        Assert.True(gauge.ClearsTick);
    }

    [Fact]
    public void LevelBelowToFinish_MissesTheTick()
    {
        var gauge = FuelGaugeCalculator.Compute(20, 65, 32);

        Assert.True(gauge.ShowTick);
        Assert.False(gauge.ClearsTick);
    }

    [Fact]
    public void ExactlyEnough_CountsAsClearing()
    {
        // The boundary matches the margin badge beside it: zero laps spare is
        // still "will finish", so the bar must not flip red at exactly enough.
        var gauge = FuelGaugeCalculator.Compute(32, 65, 32);

        Assert.True(gauge.ClearsTick);
    }

    [Fact]
    public void OverfullTank_ClampsToAFullBar()
    {
        var gauge = FuelGaugeCalculator.Compute(70, 65, null);

        Assert.Equal(1.0, gauge.FillFraction, 3);
    }

    [Fact]
    public void ToFinishBeyondCapacity_PinsTheTickToTheEnd()
    {
        // A race needing more than one tankful: the tick pins at full and the
        // level sits short of it, rather than the bar silently rescaling.
        var gauge = FuelGaugeCalculator.Compute(30, 65, 120);

        Assert.Equal(1.0, gauge.TickFraction, 3);
        Assert.False(gauge.ClearsTick);
    }

    [Fact]
    public void EmptyTank_IsAZeroFillNotAHiddenGauge()
    {
        var gauge = FuelGaugeCalculator.Compute(0, 65, 32);

        Assert.True(gauge.HasGauge);
        Assert.Equal(0, gauge.FillFraction);
        Assert.False(gauge.ClearsTick);
    }

    [Fact]
    public void NoStrategyYet_DrawsTheFillWithoutATick()
    {
        var gauge = FuelGaugeCalculator.Compute(50, 65, null);

        Assert.True(gauge.HasGauge);
        Assert.False(gauge.ShowTick);
        Assert.True(gauge.ClearsTick); // nothing to be short of yet
    }

    [Fact]
    public void NegativeFuel_ReadsAsEmpty()
    {
        var gauge = FuelGaugeCalculator.Compute(-3, 65, null);

        Assert.Equal(0, gauge.FillFraction);
    }
}
