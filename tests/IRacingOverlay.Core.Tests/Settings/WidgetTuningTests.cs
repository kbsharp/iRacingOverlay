using IRacingOverlay.Core.Radar;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class WidgetTuningTests
{
    [Fact]
    public void Defaults_MatchThePreviouslyHardcodedConstants()
    {
        // These were the literal constants in the calculators before tuning was
        // configurable. An untouched settings file must reproduce them exactly.
        var tuning = new WidgetTuning();

        Assert.Equal(0.5, tuning.FuelSafetyMarginLaps);
        Assert.Equal(60, tuning.SetupFlashSeconds);
        Assert.Equal(3, tuning.RelativeSlotsPerSide);

        // 12, not the calculator's own 30 default: 12 is what StandingsViewModel
        // has always passed, so it's the behaviour an untouched file must keep.
        Assert.Equal(12, tuning.StandingsMaxPerClass);

        // The one deliberate departure: the radar range was 60 m, shortened in the
        // density pass once the canvas scale started following the range. See
        // RadarRangeMeters' own doc comment.
        Assert.Equal(40, tuning.RadarRangeMeters);
    }

    [Fact]
    public void Defaults_RadarRange_MatchesTheCalculatorsOwnDefault()
    {
        // Two places name this number and only one of them is what the app actually
        // passes. They drifted apart once already; this is the tripwire.
        Assert.Equal(RadarCalculator.DefaultRangeMeters, new WidgetTuning().RadarRangeMeters);
    }

    [Fact]
    public void Sanitized_LeavesInBandValuesAlone()
    {
        var tuning = new WidgetTuning
        {
            FuelSafetyMarginLaps = 1.5,
            SetupFlashSeconds = 90,
            RadarRangeMeters = 45,
            RelativeSlotsPerSide = 5,
            StandingsMaxPerClass = 20,
        };

        Assert.Equal(tuning, tuning.Sanitized());
    }

    [Fact]
    public void Sanitized_ClampsOutOfBandValuesToTheBandEdge()
    {
        var tuning = new WidgetTuning
        {
            FuelSafetyMarginLaps = 99,
            SetupFlashSeconds = 0,
            RadarRangeMeters = 5000,
            RelativeSlotsPerSide = 0,
            StandingsMaxPerClass = 999,
        }.Sanitized();

        Assert.Equal(5, tuning.FuelSafetyMarginLaps);
        Assert.Equal(5, tuning.SetupFlashSeconds);
        Assert.Equal(200, tuning.RadarRangeMeters);
        Assert.Equal(1, tuning.RelativeSlotsPerSide);
        Assert.Equal(60, tuning.StandingsMaxPerClass);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Sanitized_NonFiniteFallsBackToTheDefault_NotABandEdge(double value)
    {
        // A band edge would be an arbitrary choice for a value that isn't a
        // number at all - the default is the honest answer.
        var tuning = new WidgetTuning
        {
            FuelSafetyMarginLaps = value,
            SetupFlashSeconds = value,
            RadarRangeMeters = value,
        }.Sanitized();

        Assert.Equal(0.5, tuning.FuelSafetyMarginLaps);
        Assert.Equal(60, tuning.SetupFlashSeconds);
        Assert.Equal(40, tuning.RadarRangeMeters);
    }
}
