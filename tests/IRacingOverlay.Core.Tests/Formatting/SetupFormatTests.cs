using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Tests.Formatting;

public class SetupFormatTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_NullOrBlank_ReturnsPlaceholder(string? setupName)
    {
        Assert.Equal(TelemetryFormat.Placeholder, SetupFormat.DisplayName(setupName));
    }

    [Fact]
    public void DisplayName_StripsStoExtension()
    {
        Assert.Equal("race_setup", SetupFormat.DisplayName("race_setup.sto"));
    }

    [Fact]
    public void DisplayName_ExtensionMatchIsCaseInsensitive()
    {
        Assert.Equal("RACE_SETUP", SetupFormat.DisplayName("RACE_SETUP.STO"));
    }

    [Fact]
    public void DisplayName_NoExtension_ReturnsUnchanged()
    {
        Assert.Equal("baseline", SetupFormat.DisplayName("baseline"));
    }

    [Fact]
    public void DisplayName_TrimsSurroundingWhitespace()
    {
        Assert.Equal("race_setup", SetupFormat.DisplayName("  race_setup.sto  "));
    }
}
