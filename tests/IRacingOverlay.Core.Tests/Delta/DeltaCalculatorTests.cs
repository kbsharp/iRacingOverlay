using IRacingOverlay.Core.Delta;

namespace IRacingOverlay.Core.Tests.Delta;

public class DeltaCalculatorTests
{
    private const int Session = 1;

    private readonly DeltaCalculator _calculator = new();

    private DeltaReading Update(
        int lap,
        double time,
        double delta,
        bool valid = true,
        bool onTrack = true,
        bool inPits = false,
        int session = Session)
        => _calculator.Update(session, lap, time, delta, valid, onTrack, inPits);

    [Fact]
    public void ReportsNothingBeforeThereIsAReferenceLap()
    {
        var reading = Update(lap: 1, time: 10, delta: 0, valid: false);

        Assert.Equal(DeltaState.None, reading.State);
        Assert.False(reading.HasDelta);
    }

    [Fact]
    public void ReportsTheLiveDeltaOnceValid()
    {
        var reading = Update(lap: 2, time: 100, delta: -0.34);

        Assert.Equal(DeltaState.Live, reading.State);
        Assert.Equal(-0.34, reading.Seconds, 3);
        Assert.Equal(DeltaTone.Faster, reading.Tone);
    }

    [Theory]
    [InlineData(-0.6, DeltaTone.Faster)]
    [InlineData(0.6, DeltaTone.Slower)]
    [InlineData(-0.04, DeltaTone.Neutral)]
    [InlineData(0.04, DeltaTone.Neutral)]
    public void ColoursByDirectionOutsideTheDeadband(double delta, DeltaTone expected)
    {
        Assert.Equal(expected, Update(lap: 2, time: 100, delta: delta).Tone);
    }

    [Theory]
    [InlineData(-0.5, 0.5)]
    [InlineData(0.25, 0.25)]
    [InlineData(-2.4, 1.0)]  // clamped at full scale
    [InlineData(3.0, 1.0)]
    public void FillsTheBarByMagnitudeAndClampsAtFullScale(double delta, double expected)
    {
        Assert.Equal(expected, Update(lap: 2, time: 100, delta: delta).BarFraction, 3);
    }

    [Fact]
    public void HoldsTheFinishedLapsDeltaAcrossTheLine()
    {
        Update(lap: 2, time: 100, delta: -0.42);

        // The sim snaps its delta back towards zero for the new lap; the number
        // worth reading is the one the last lap ended on.
        var atLine = Update(lap: 3, time: 100.1, delta: 0.01);

        Assert.Equal(DeltaState.LapComplete, atLine.State);
        Assert.Equal(-0.42, atLine.Seconds, 3);
        Assert.Equal(DeltaTone.Faster, atLine.Tone);
    }

    [Fact]
    public void ReleasesTheHeldDeltaAfterTheHoldWindow()
    {
        Update(lap: 2, time: 100, delta: -0.42);
        Update(lap: 3, time: 100.1, delta: 0.01);

        var after = Update(lap: 3, time: 100.1 + DeltaCalculator.HoldSeconds, delta: 0.2);

        Assert.Equal(DeltaState.Live, after.State);
        Assert.Equal(0.2, after.Seconds, 3);
    }

    [Fact]
    public void DoesNotHoldWhenTheLapCounterGoesBackwards()
    {
        // A tow back to the pits or a session reset rewinds the lap; that is not
        // a lap completed and there is no result to show.
        Update(lap: 5, time: 100, delta: -0.42);

        var reading = Update(lap: 1, time: 101, delta: 0.1);

        Assert.Equal(DeltaState.Live, reading.State);
        Assert.Equal(0.1, reading.Seconds, 3);
    }

    [Fact]
    public void ReportsNothingInThePits()
    {
        Update(lap: 2, time: 100, delta: -0.4);

        Assert.False(Update(lap: 2, time: 101, delta: -0.4, inPits: true).HasDelta);
    }

    [Fact]
    public void ReportsNothingWhileOutOfTheCar()
    {
        Update(lap: 2, time: 100, delta: -0.4);

        Assert.False(Update(lap: 2, time: 101, delta: -0.4, onTrack: false).HasDelta);
    }

    [Fact]
    public void DropsAHeldDeltaOnPitEntry()
    {
        Update(lap: 2, time: 100, delta: -0.42);
        Update(lap: 3, time: 100.1, delta: 0.01);
        Update(lap: 3, time: 100.2, delta: 0.01, inPits: true);

        // Back on track well inside the old hold window: the in-lap's result must
        // not reappear.
        var reading = Update(lap: 3, time: 101, delta: 0.05);

        Assert.Equal(DeltaState.Live, reading.State);
        Assert.Equal(0.05, reading.Seconds, 3);
    }

    [Fact]
    public void ForgetsEverythingWhenTheSessionChanges()
    {
        Update(lap: 8, time: 400, delta: -0.42);

        // Qualifying to race: a new reference lap, and the previous session's
        // lap counter must not read as a lap completed here.
        var reading = Update(lap: 1, time: 0, delta: 0, valid: false, session: Session + 1);

        Assert.Equal(DeltaState.None, reading.State);
    }
}
