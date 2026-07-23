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
        // The shape the app shipped with. It must still load, and every widget that
        // ships on must still be on - a silently disabled overlay after an update
        // would be indistinguishable from a broken one. The delta bar and track map
        // are the opt-in widgets, so they stay off for a legacy file just as for a
        // fresh one.
        var optIn = new[] { WidgetIds.Delta, WidgetIds.TrackMap };
        var json = """{ "scale": 1.25, "windows": { "FuelWindow": { "left": 80, "top": 140 } } }""";

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.25, settings.Scale);
        Assert.Equal(new WindowPosition(80, 140), settings.Windows["FuelWindow"]);
        Assert.All(
            WidgetIds.All.Where(id => !optIn.Contains(id)),
            id => Assert.True(settings.IsWidgetEnabled(id)));
        Assert.All(optIn, id => Assert.False(settings.IsWidgetEnabled(id)));
        Assert.All(WidgetIds.All, id => Assert.False(settings.IsClickThrough(id)));
        Assert.Equal(new UnitPreferences(), settings.Units);
        Assert.Equal(new WidgetTuning(), settings.Tuning);
        Assert.False(settings.RunAtStartup);
        // Absent from an old file, the poll rate must land on the 30 Hz default,
        // not the 0 an int property would deserialize to and then sanitize to 10.
        Assert.Equal(30, settings.TelemetryRefreshHz);
    }

    [Fact]
    public void Deserialize_TelemetryRefreshHz_RoundTripsAnOfferedRate()
    {
        var json = OverlaySettingsSerializer.Serialize(new OverlaySettings { TelemetryRefreshHz = 60 });

        Assert.Equal(60, OverlaySettingsSerializer.Deserialize(json).TelemetryRefreshHz);
    }

    [Fact]
    public void Deserialize_TelemetryRefreshHz_OffListValueIsSnappedToAnOfferedRate()
    {
        // A hand-edited 45 isn't an achievable divisor of the 60 Hz broadcast, so it
        // must be snapped rather than persisted as a rate the SDK can't deliver.
        var settings = OverlaySettingsSerializer.Deserialize("""{ "telemetryRefreshHz": 45 }""");

        Assert.Contains(settings.TelemetryRefreshHz, IRacingOverlay.Core.Telemetry.TelemetryRefresh.AllowedHz);
    }

    [Fact]
    public void Deserialize_TelemetryRefreshHz_ZeroIsSanitizedNotLeftToDivideByZero()
    {
        var settings = OverlaySettingsSerializer.Deserialize("""{ "telemetryRefreshHz": 0 }""");

        Assert.Contains(settings.TelemetryRefreshHz, IRacingOverlay.Core.Telemetry.TelemetryRefresh.AllowedHz);
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

    /// <summary>
    /// The safety/CPI chip was removed, but every settings.json written while it
    /// existed still carries its accumulated baseline. That file has to keep
    /// loading - a stale field must not cost the user their layout.
    /// </summary>
    [Fact]
    public void SafetyHistory_FromAFileWrittenBeforeItWasRemoved_IsIgnored()
    {
        var json = """
            {
              "scale": 1.25,
              "safetyHistory": { "corners": 1200, "incidentPoints": 8 }
            }
            """;

        var settings = OverlaySettingsSerializer.Deserialize(json);

        Assert.Equal(1.25, settings.Scale);
    }
}
