using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Tests.Strategy;

public class SaveCostTrackerTests
{
    [Fact]
    public void NoLaps_HasNoEstimate()
    {
        var tracker = new SaveCostTracker();

        Assert.Equal(0, tracker.SampleCount);
        Assert.False(tracker.Cost.IsKnown);
    }

    [Fact]
    public void FewerThanSixCleanLaps_HasNoEstimate()
    {
        var tracker = new SaveCostTracker();

        // A clean, well-spread relationship - just not enough of it.
        DriveLaps(tracker, [2.0, 2.4, 2.0, 2.4, 2.0], baseLapSeconds: 90, secondsPerLiter: 2.0);

        Assert.Equal(5, tracker.SampleCount);
        Assert.False(tracker.Cost.IsKnown);
    }

    [Fact]
    public void SixLapsWithSpread_LearnsTheExchangeRate()
    {
        var tracker = new SaveCostTracker();

        // Lap time falls 2s for every extra litre burned; the tracker should
        // recover exactly that, since the relationship is noiseless here.
        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 90, secondsPerLiter: 2.0);

        var cost = tracker.Cost;

        Assert.True(cost.IsKnown);
        Assert.Equal(2.0, cost.SecondsPerLiter, precision: 3);
        Assert.Equal(2.0, cost.LowestObservedLitersPerLap, precision: 3);
        Assert.Equal(0.5, cost.ObservedSpreadLitersPerLap, precision: 3);
    }

    [Fact]
    public void LapsAllDrivenTheSameWay_HasNoEstimate()
    {
        var tracker = new SaveCostTracker();

        // A 1% spread of burn: nobody was saving, so there is no exchange rate to
        // read - only lap-time noise divided by almost nothing.
        DriveLaps(tracker, [2.50, 2.51, 2.50, 2.52, 2.50, 2.51], baseLapSeconds: 90, secondsPerLiter: 2.0);

        Assert.Equal(6, tracker.SampleCount);
        Assert.False(tracker.Cost.IsKnown);
    }

    [Fact]
    public void BurningMoreButGoingSlower_HasNoEstimate()
    {
        var tracker = new SaveCostTracker();

        // The relationship inverted: this is noise beating signal, not a discovery
        // that fuel saving is free.
        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 90, secondsPerLiter: -2.0);

        Assert.False(tracker.Cost.IsKnown);
    }

    [Fact]
    public void WildExchangeRate_CannotSurviveTheOutlierFilter()
    {
        var tracker = new SaveCostTracker();

        // A 40 s/L relationship across a 0.5 L range means 20s between the leanest
        // and thirstiest laps - so all but one of them are outliers by the 5% rule,
        // and what's left is too small a sample to fit. The two guards together are
        // what bound how steep a reported rate can get.
        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 120, secondsPerLiter: 40);

        Assert.Equal(6, tracker.SampleCount);
        Assert.False(tracker.Cost.IsKnown);
    }

    [Fact]
    public void SlowOutlierLap_DoesNotFlipTheFit()
    {
        var tracker = new SaveCostTracker();

        // Six honest laps, then a spin: it burned the least fuel *and* took 20s
        // longer, which is the opposite of the relationship being measured. Left
        // in, it drags the slope positive and the estimate disappears.
        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 90, secondsPerLiter: 2.0);
        DriveLap(tracker, liters: 1.6, lapSeconds: 110);

        var cost = tracker.Cost;

        Assert.Equal(7, tracker.SampleCount);
        Assert.True(cost.IsKnown);
        Assert.Equal(2.0, cost.SecondsPerLiter, precision: 3);

        // And the discarded lap doesn't widen the observed range either - the
        // range has to describe laps the fit was actually made from.
        Assert.Equal(2.0, cost.LowestObservedLitersPerLap, precision: 3);
    }

    [Fact]
    public void RefuelledLap_IsNotRecorded()
    {
        var tracker = new SaveCostTracker();

        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 90, secondsPerLiter: 2.0);
        var before = tracker.SampleCount;

        // Mid-lap fuel gain: the burn measurement for this lap is meaningless.
        var lap = 100;
        tracker.Update(lap, 20f, 1000, onPitRoad: false);
        tracker.Update(lap, 40f, 1030, onPitRoad: false);
        tracker.Update(lap + 1, 38f, 1090, onPitRoad: false);

        Assert.Equal(before, tracker.SampleCount);
    }

    [Fact]
    public void LapThatTouchedPitRoad_IsNotRecorded()
    {
        var tracker = new SaveCostTracker();

        var lap = 5;
        tracker.Update(lap, 30f, 0, onPitRoad: false);
        tracker.Update(lap, 29f, 45, onPitRoad: true);
        tracker.Update(lap + 1, 28f, 120, onPitRoad: false);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void LapStartingInThePitLane_IsNotRecorded()
    {
        var tracker = new SaveCostTracker();

        // The out-lap: it begins in the lane, so it is neither a normal burn nor a
        // normal lap time even though every later frame is on track.
        tracker.Update(6, 30f, 0, onPitRoad: true);
        tracker.Update(6, 29.5f, 30, onPitRoad: false);
        tracker.Update(7, 28f, 120, onPitRoad: false);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void SkippedLap_IsNotRecorded()
    {
        var tracker = new SaveCostTracker();

        tracker.Update(5, 30f, 0, onPitRoad: false);
        tracker.Update(7, 27f, 180, onPitRoad: false);

        Assert.Equal(0, tracker.SampleCount);
    }

    [Fact]
    public void WindowKeepsOnlyTheLastTwelveLaps()
    {
        var tracker = new SaveCostTracker();

        DriveLaps(
            tracker,
            [2.0, 2.2, 2.4, 2.1, 2.3, 2.5, 2.0, 2.2, 2.4, 2.1, 2.3, 2.5, 2.0, 2.2],
            baseLapSeconds: 90,
            secondsPerLiter: 2.0);

        Assert.Equal(12, tracker.SampleCount);
    }

    [Fact]
    public void Reset_ForgetsEverything()
    {
        var tracker = new SaveCostTracker();

        DriveLaps(tracker, [2.0, 2.2, 2.4, 2.1, 2.3, 2.5], baseLapSeconds: 90, secondsPerLiter: 2.0);
        tracker.Reset();

        Assert.Equal(0, tracker.SampleCount);
        Assert.False(tracker.Cost.IsKnown);
    }

    // Per-test state: xUnit builds a fresh instance for every fact, so each one
    // starts with a full tank on lap zero.
    private double _fuel = 60;
    private int _lap;
    private double _time;

    /// <summary>
    /// Drives clean laps burning the given litres, where each extra litre buys
    /// <paramref name="secondsPerLiter"/> of lap time back.
    /// </summary>
    private void DriveLaps(
        SaveCostTracker tracker, double[] liters, double baseLapSeconds, double secondsPerLiter)
    {
        var reference = liters[0];

        foreach (var burn in liters)
        {
            DriveLap(tracker, burn, baseLapSeconds - secondsPerLiter * (burn - reference));
        }
    }

    private void DriveLap(SaveCostTracker tracker, double liters, double lapSeconds)
    {
        tracker.Update(_lap, (float)_fuel, _time, onPitRoad: false);

        _lap++;
        _time += lapSeconds;
        _fuel -= liters;

        tracker.Update(_lap, (float)_fuel, _time, onPitRoad: false);
    }
}
