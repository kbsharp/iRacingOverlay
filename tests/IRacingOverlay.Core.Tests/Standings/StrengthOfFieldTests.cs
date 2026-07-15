using IRacingOverlay.Core.Standings;

namespace IRacingOverlay.Core.Tests.Standings;

public class StrengthOfFieldTests
{
    [Fact]
    public void Compute_UniformField_EqualsThatIRating()
    {
        Assert.Equal(2500, StrengthOfField.Compute([2500, 2500, 2500, 2500]));
    }

    [Fact]
    public void Compute_Empty_IsZero()
    {
        Assert.Equal(0, StrengthOfField.Compute([]));
    }

    [Fact]
    public void Compute_IgnoresNonPositiveRatings()
    {
        // The zeros (no iRating known) drop out, leaving a uniform 3000 field.
        Assert.Equal(3000, StrengthOfField.Compute([3000, 0, 3000, -1, 3000]));
    }

    [Fact]
    public void Compute_WeightsLowerRatingsMoreThanAPlainMean()
    {
        // Arithmetic mean of 1000 and 3000 is 2000; SoF sits below it.
        var sof = StrengthOfField.Compute([1000, 3000]);

        Assert.InRange(sof, 1785, 1795);
    }
}
