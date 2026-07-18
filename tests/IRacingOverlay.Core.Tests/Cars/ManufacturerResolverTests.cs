using IRacingOverlay.Core.Cars;

namespace IRacingOverlay.Core.Tests.Cars;

public class ManufacturerResolverTests
{
    [Theory]
    // Real CarPath folder tokens across the GT3, GTP, LMP2 and cup fields.
    [InlineData("acuransxevo22gt3", Manufacturer.Acura)]
    [InlineData("astonmartinvantagegt3", Manufacturer.AstonMartin)]
    [InlineData("audir8lmsevo2gt3", Manufacturer.Audi)]
    [InlineData("bmwm4gt3", Manufacturer.Bmw)]
    [InlineData("cadillacvseriesrgtp", Manufacturer.Cadillac)]
    [InlineData("chevyvettez06rgt3", Manufacturer.Chevrolet)]
    [InlineData("dallarap217", Manufacturer.Dallara)]
    [InlineData("ferrari296gt3", Manufacturer.Ferrari)]
    [InlineData("ferrari499p", Manufacturer.Ferrari)]
    [InlineData("fordmustanggt3", Manufacturer.Ford)]
    [InlineData("hyundaielantracn7", Manufacturer.Hyundai)]
    [InlineData("lamborghinihuracangt3evo", Manufacturer.Lamborghini)]
    [InlineData("lexusrcfgt3", Manufacturer.Lexus)]
    [InlineData("ligierjsp320", Manufacturer.Ligier)]
    [InlineData("mclaren720sgt3", Manufacturer.McLaren)]
    [InlineData("mercedesamgevogt3", Manufacturer.Mercedes)]
    [InlineData("mx5 mx52016", Manufacturer.Mazda)]
    [InlineData("porsche992rgt3", Manufacturer.Porsche)]
    [InlineData("porsche992cup", Manufacturer.Porsche)]
    [InlineData("radicalsr8", Manufacturer.Radical)]
    [InlineData("rufrt12r", Manufacturer.Ruf)]
    public void Resolve_ReadsManufacturerFromCarPath(string carPath, Manufacturer expected)
    {
        Assert.Equal(expected, ManufacturerResolver.Resolve(carPath));
    }

    [Theory]
    [InlineData("BMWM4GT3", Manufacturer.Bmw)]
    [InlineData("Ferrari296GT3", Manufacturer.Ferrari)]
    public void Resolve_IsCaseInsensitive(string carPath, Manufacturer expected)
    {
        Assert.Equal(expected, ManufacturerResolver.Resolve(carPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("somefuturecar2027")] // a car we don't map yet must degrade, not throw
    public void Resolve_UnknownOrMissing_ReturnsUnknown(string? carPath)
    {
        Assert.Equal(Manufacturer.Unknown, ManufacturerResolver.Resolve(carPath));
    }

    [Fact]
    public void Resolve_FallsBackToScreenName_WhenPathHasNoMatch()
    {
        Assert.Equal(
            Manufacturer.Porsche,
            ManufacturerResolver.Resolve(carPath: "unknownpath9000", carScreenName: "Porsche 911 GT3 R (992)"));
    }

    [Fact]
    public void Resolve_PrefersCarPath_OverScreenName()
    {
        // A confident path match wins even if the screen name is blank.
        Assert.Equal(
            Manufacturer.Ferrari,
            ManufacturerResolver.Resolve(carPath: "ferrari296gt3", carScreenName: null));
    }
}
