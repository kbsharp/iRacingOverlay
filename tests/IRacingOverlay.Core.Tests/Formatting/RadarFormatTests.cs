using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Formatting;

public class RadarFormatTests
{
    [Theory]
    [InlineData(CarLeftRight.CarLeft, true)]
    [InlineData(CarLeftRight.CarLeftRight, true)]
    [InlineData(CarLeftRight.TwoCarsLeft, true)]
    [InlineData(CarLeftRight.CarRight, false)]
    [InlineData(CarLeftRight.TwoCarsRight, false)]
    [InlineData(CarLeftRight.Clear, false)]
    [InlineData(CarLeftRight.Off, false)]
    public void HasCarLeft_ClassifiesCorrectly(CarLeftRight state, bool expected)
    {
        Assert.Equal(expected, RadarFormat.HasCarLeft(state));
    }

    [Theory]
    [InlineData(CarLeftRight.CarRight, true)]
    [InlineData(CarLeftRight.CarLeftRight, true)]
    [InlineData(CarLeftRight.TwoCarsRight, true)]
    [InlineData(CarLeftRight.CarLeft, false)]
    [InlineData(CarLeftRight.TwoCarsLeft, false)]
    [InlineData(CarLeftRight.Clear, false)]
    [InlineData(CarLeftRight.Off, false)]
    public void HasCarRight_ClassifiesCorrectly(CarLeftRight state, bool expected)
    {
        Assert.Equal(expected, RadarFormat.HasCarRight(state));
    }

    [Theory]
    [InlineData(CarLeftRight.TwoCarsLeft, true)]
    [InlineData(CarLeftRight.CarLeft, false)]
    [InlineData(CarLeftRight.TwoCarsRight, false)]
    public void HasTwoCarsLeft_OnlyTrueForTwoCarsLeft(CarLeftRight state, bool expected)
    {
        Assert.Equal(expected, RadarFormat.HasTwoCarsLeft(state));
    }

    [Theory]
    [InlineData(CarLeftRight.TwoCarsRight, true)]
    [InlineData(CarLeftRight.CarRight, false)]
    [InlineData(CarLeftRight.TwoCarsLeft, false)]
    public void HasTwoCarsRight_OnlyTrueForTwoCarsRight(CarLeftRight state, bool expected)
    {
        Assert.Equal(expected, RadarFormat.HasTwoCarsRight(state));
    }

    [Theory]
    [InlineData(CarLeftRight.Off, false)]
    [InlineData(CarLeftRight.Clear, true)]
    [InlineData(CarLeftRight.CarLeft, true)]
    public void IsActive_FalseOnlyWhenOff(CarLeftRight state, bool expected)
    {
        Assert.Equal(expected, RadarFormat.IsActive(state));
    }
}
