using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class OverlaySettingsSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesScaleAndWindowPositions()
    {
        var original = new OverlaySettings
        {
            Scale = 1.25,
            Windows = new Dictionary<string, WindowPosition>
            {
                ["StandingsWindow"] = new(24, 24),
                ["RelativeWindow"] = new(-1920.5, 760),
            },
        };

        var restored = OverlaySettingsSerializer.Deserialize(OverlaySettingsSerializer.Serialize(original));

        Assert.Equal(1.25, restored.Scale);
        Assert.Equal(new WindowPosition(24, 24), restored.Windows["StandingsWindow"]);
        Assert.Equal(new WindowPosition(-1920.5, 760), restored.Windows["RelativeWindow"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not valid json")]
    [InlineData("[]")]
    public void Deserialize_MissingOrCorrupt_ReturnsDefaults(string? json)
    {
        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.0, settings.Scale);
        Assert.Empty(settings.Windows);
    }

    [Fact]
    public void Deserialize_MissingScaleField_DefaultsTo100Percent()
    {
        var settings = OverlaySettingsSerializer.Deserialize("""{ "windows": {} }""");

        Assert.Equal(1.0, settings.Scale);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(50.0)]
    public void Deserialize_OutOfRangeScale_SanitizedTo100Percent(double badScale)
    {
        var json = $$"""{ "scale": {{badScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.0, settings.Scale);
    }

    [Fact]
    public void Deserialize_UnknownExtraFields_AreIgnored()
    {
        var json = """{ "scale": 1.5, "windows": {}, "somethingNew": 42 }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.5, settings.Scale);
    }
}
