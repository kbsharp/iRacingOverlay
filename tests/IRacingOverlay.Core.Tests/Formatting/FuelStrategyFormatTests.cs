using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Fuel;

namespace IRacingOverlay.Core.Tests.Formatting;

public class FuelStrategyFormatTests
{
    private static FuelStrategy WithStops(int additionalStops) =>
        FuelStrategy.Unknown with { AdditionalStops = additionalStops };

    [Fact]
    public void AdditionalStops_ZeroOrFewer_IsEmpty()
    {
        Assert.Equal(string.Empty, FuelStrategyFormat.AdditionalStops(WithStops(0)));
        Assert.Equal(string.Empty, FuelStrategyFormat.AdditionalStops(WithStops(-1)));
    }

    [Fact]
    public void AdditionalStops_One_IsSingular()
    {
        Assert.Equal("+1 stop", FuelStrategyFormat.AdditionalStops(WithStops(1)));
    }

    [Fact]
    public void AdditionalStops_Many_IsPlural()
    {
        Assert.Equal("+3 stops", FuelStrategyFormat.AdditionalStops(WithStops(3)));
    }
}
