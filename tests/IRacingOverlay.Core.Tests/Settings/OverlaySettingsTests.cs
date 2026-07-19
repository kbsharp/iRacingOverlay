using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class OverlaySettingsTests
{
    // The per-widget maps are sparse by design: an absent key must mean "default",
    // so adding a widget can't switch itself off for existing users, and a fresh
    // install writes almost nothing.

    [Fact]
    public void IsWidgetEnabled_NoEntry_DefaultsToEnabled()
        => Assert.True(new OverlaySettings().IsWidgetEnabled(WidgetIds.Radar));

    [Fact]
    public void IsWidgetEnabled_ExplicitlyDisabled_IsFalse()
    {
        var settings = new OverlaySettings
        {
            EnabledWidgets = new Dictionary<string, bool> { [WidgetIds.Radar] = false },
        };

        Assert.False(settings.IsWidgetEnabled(WidgetIds.Radar));
        Assert.True(settings.IsWidgetEnabled(WidgetIds.Fuel));
    }

    // The setup readout used to be a widget of its own, on by default. Folding it
    // into the fuel panel must not take it away from anyone who had it, so the new
    // toggle defaults to on - including for a settings file written before the
    // property existed, which is the case that matters for the shipped app.

    [Fact]
    public void ShowSetupReminder_DefaultsToOn()
        => Assert.True(new OverlaySettings().ShowSetupReminder);

    [Fact]
    public void ShowSetupReminder_AbsentFromExistingFile_StaysOn()
    {
        var restored = OverlaySettingsSerializer.Deserialize("""{ "scale": 1.0 }""");

        Assert.True(restored.ShowSetupReminder);
    }

    [Fact]
    public void ShowSetupReminder_SwitchedOff_RoundTrips()
    {
        var json = OverlaySettingsSerializer.Serialize(new OverlaySettings { ShowSetupReminder = false });

        Assert.False(OverlaySettingsSerializer.Deserialize(json).ShowSetupReminder);
    }

    // The manufacturer badges are the other way round: opt-in while the mark set
    // is incomplete, so an existing settings file must not switch them on.

    [Fact]
    public void ShowManufacturerBadges_DefaultsToOff()
        => Assert.False(new OverlaySettings().ShowManufacturerBadges);

    [Fact]
    public void ShowManufacturerBadges_AbsentFromExistingFile_StaysOff()
    {
        var restored = OverlaySettingsSerializer.Deserialize("""{ "scale": 1.0 }""");

        Assert.False(restored.ShowManufacturerBadges);
    }

    [Fact]
    public void ShowManufacturerBadges_SwitchedOn_RoundTrips()
    {
        var json = OverlaySettingsSerializer.Serialize(new OverlaySettings { ShowManufacturerBadges = true });

        Assert.True(OverlaySettingsSerializer.Deserialize(json).ShowManufacturerBadges);
    }

    [Fact]
    public void WidgetIds_NoLongerListsTheStandaloneSetupWidget()
    {
        // Its settings key ("SetupWindow") may still sit in an existing file; what
        // matters is that no settings surface offers it as a widget any more.
        Assert.DoesNotContain("SetupWindow", WidgetIds.All);
        Assert.Equal(4, WidgetIds.All.Count);
    }

    [Fact]
    public void ScaleFor_NoOverride_FallsBackToSharedScale()
    {
        var settings = new OverlaySettings { Scale = 1.25 };

        Assert.Equal(1.25, settings.ScaleFor(WidgetIds.Standings));
    }

    [Fact]
    public void ScaleFor_WithOverride_UsesTheOverride()
    {
        var settings = new OverlaySettings
        {
            Scale = 1.25,
            WidgetScales = new Dictionary<string, double> { [WidgetIds.Radar] = 1.75 },
        };

        Assert.Equal(1.75, settings.ScaleFor(WidgetIds.Radar));
        Assert.Equal(1.25, settings.ScaleFor(WidgetIds.Standings));
    }

    [Fact]
    public void IsClickThrough_NoEntry_DefaultsToInteractive()
        => Assert.False(new OverlaySettings().IsClickThrough(WidgetIds.Relative));

    [Fact]
    public void IsClickThrough_ExplicitlyEnabled_IsTrue()
    {
        var settings = new OverlaySettings
        {
            ClickThroughWidgets = new Dictionary<string, bool> { [WidgetIds.Relative] = true },
        };

        Assert.True(settings.IsClickThrough(WidgetIds.Relative));
    }

    [Fact]
    public void WidgetIds_AllAreDistinct()
        => Assert.Equal(WidgetIds.All.Count, WidgetIds.All.Distinct().Count());
}
