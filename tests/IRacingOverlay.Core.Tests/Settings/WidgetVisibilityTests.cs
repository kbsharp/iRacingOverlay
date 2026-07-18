using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class WidgetVisibilityTests
{
    [Fact]
    public void Shows_when_enabled_and_sim_connected()
    {
        Assert.True(WidgetVisibility.ShouldShow(isEnabled: true, isSimConnected: true, hideWhenSimClosed: true));
    }

    [Fact]
    public void Hides_when_sim_is_closed()
    {
        // The "start with Windows" case: nothing on screen until iRacing runs.
        Assert.False(WidgetVisibility.ShouldShow(isEnabled: true, isSimConnected: false, hideWhenSimClosed: true));
    }

    [Fact]
    public void Shows_with_sim_closed_when_the_user_has_opted_out()
    {
        Assert.True(WidgetVisibility.ShouldShow(isEnabled: true, isSimConnected: false, hideWhenSimClosed: false));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void A_disabled_widget_never_shows(bool isSimConnected, bool hideWhenSimClosed)
    {
        Assert.False(WidgetVisibility.ShouldShow(isEnabled: false, isSimConnected, hideWhenSimClosed));
    }

    [Fact]
    public void Hiding_when_the_sim_is_closed_is_the_default()
    {
        Assert.True(new OverlaySettings().HideWhenSimClosed);
    }

    [Fact]
    public void Existing_settings_files_without_the_key_keep_the_default()
    {
        var restored = OverlaySettingsSerializer.Deserialize("""{ "Scale": 1.0 }""");

        Assert.True(restored.HideWhenSimClosed);
    }

    [Fact]
    public void Round_trips_the_opt_out()
    {
        var json = OverlaySettingsSerializer.Serialize(new OverlaySettings { HideWhenSimClosed = false });

        Assert.False(OverlaySettingsSerializer.Deserialize(json).HideWhenSimClosed);
    }
}
