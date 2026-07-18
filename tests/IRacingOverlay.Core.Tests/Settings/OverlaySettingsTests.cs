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
