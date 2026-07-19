using IRacingOverlay.Core.Rating;
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

    [Fact]
    public void RoundTrip_PreservesTheWidgetPreferences()
    {
        var original = new OverlaySettings
        {
            EnabledWidgets = new Dictionary<string, bool> { [WidgetIds.Radar] = false },
            WidgetScales = new Dictionary<string, double> { [WidgetIds.Standings] = 1.5 },
            ClickThroughWidgets = new Dictionary<string, bool> { [WidgetIds.Relative] = true },
            Units = new UnitPreferences { Fuel = FuelUnit.Gallons, Temperature = TemperatureUnit.Fahrenheit },
            Tuning = new WidgetTuning { RadarRangeMeters = 80, RelativeSlotsPerSide = 4 },
            RunAtStartup = true,
        };

        var restored = OverlaySettingsSerializer.Deserialize(OverlaySettingsSerializer.Serialize(original));

        Assert.False(restored.IsWidgetEnabled(WidgetIds.Radar));
        Assert.Equal(1.5, restored.ScaleFor(WidgetIds.Standings));
        Assert.True(restored.IsClickThrough(WidgetIds.Relative));
        Assert.Equal(FuelUnit.Gallons, restored.Units.Fuel);
        Assert.Equal(TemperatureUnit.Fahrenheit, restored.Units.Temperature);
        Assert.Equal(80, restored.Tuning.RadarRangeMeters);
        Assert.Equal(4, restored.Tuning.RelativeSlotsPerSide);
        Assert.True(restored.RunAtStartup);
    }

    [Fact]
    public void Deserialize_SettingsFileFromBeforeTheseFieldsExisted_GetsDefaults()
    {
        // The shape the app shipped with. It must still load, and every widget
        // must still be on - a silently disabled overlay after an update would be
        // indistinguishable from a broken one.
        var json = """{ "scale": 1.25, "windows": { "FuelWindow": { "left": 80, "top": 140 } } }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.25, settings.Scale);
        Assert.Equal(new WindowPosition(80, 140), settings.Windows["FuelWindow"]);
        Assert.All(WidgetIds.All, id => Assert.True(settings.IsWidgetEnabled(id)));
        Assert.All(WidgetIds.All, id => Assert.False(settings.IsClickThrough(id)));
        Assert.Equal(new UnitPreferences(), settings.Units);
        Assert.Equal(new WidgetTuning(), settings.Tuning);
        Assert.False(settings.RunAtStartup);
    }

    [Fact]
    public void Deserialize_NullMapsAndNestedRecords_BecomeEmptyDefaults()
    {
        var json = """
            {
              "windows": null, "enabledWidgets": null, "widgetScales": null,
              "clickThroughWidgets": null, "units": null, "tuning": null
            }
            """;

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Empty(settings.Windows);
        Assert.Empty(settings.EnabledWidgets);
        Assert.Empty(settings.WidgetScales);
        Assert.Empty(settings.ClickThroughWidgets);
        Assert.Equal(new UnitPreferences(), settings.Units);
        Assert.Equal(new WidgetTuning(), settings.Tuning);
    }

    [Fact]
    public void Deserialize_OutOfRangePerWidgetScale_IsSanitizedLikeTheGlobalOne()
    {
        var json = """{ "widgetScales": { "RadarWindow": 0.01, "FuelWindow": 1.5 } }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.0, settings.ScaleFor(WidgetIds.Radar));
        Assert.Equal(1.5, settings.ScaleFor(WidgetIds.Fuel));
    }

    [Fact]
    public void Deserialize_OutOfRangeTuning_IsClamped()
    {
        var json = """{ "tuning": { "radarRangeMeters": 99999, "relativeSlotsPerSide": 0 } }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(200, settings.Tuning.RadarRangeMeters);
        Assert.Equal(1, settings.Tuning.RelativeSlotsPerSide);
    }

    [Fact]
    public void SafetyHistory_SurvivesARoundTrip()
    {
        var json = OverlaySettingsSerializer.Serialize(
            new OverlaySettings { SafetyHistory = new CpiHistory(1200, 8) });

        var restored = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1200, restored.SafetyHistory.Corners);
        Assert.Equal(8, restored.SafetyHistory.IncidentPoints);
    }

    [Fact]
    public void SafetyHistory_AbsentFromAnOlderFile_StartsEmpty()
    {
        var settings = OverlaySettingsSerializer.Deserialize("""{ "scale": 1.0 }""");

        Assert.Equal(CpiHistory.Empty, settings.SafetyHistory);
    }

    /// <summary>
    /// The baseline is earned data, not a preference. A value that couldn't have
    /// been accumulated means a hand-edited or foreign file, and judging real
    /// sessions against it would be worse than starting over.
    /// </summary>
    [Theory]
    [InlineData(-100, 2)]
    [InlineData(500, -1)]
    [InlineData(999999, 4)]
    public void SafetyHistory_ImpossibleValues_AreReset(double corners, double incidents)
    {
        var json = OverlaySettingsSerializer.Serialize(
            new OverlaySettings { SafetyHistory = new CpiHistory(corners, incidents) });

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(CpiHistory.Empty, settings.SafetyHistory);
    }
}
