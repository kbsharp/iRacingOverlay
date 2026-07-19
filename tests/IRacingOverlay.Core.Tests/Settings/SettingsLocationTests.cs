using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class SettingsLocationTests
{
    [Fact]
    public void The_installed_copy_keeps_the_original_file_name()
    {
        // Load-bearing: an existing user's layout lives in settings.json, and
        // renaming it here would silently reset everyone's widget positions.
        Assert.Equal("settings.json", SettingsLocation.FileNameFor(isInstalled: true));
    }

    [Fact]
    public void A_source_or_portable_build_gets_its_own_file()
    {
        Assert.Equal("settings.dev.json", SettingsLocation.FileNameFor(isInstalled: false));
    }

    [Fact]
    public void The_two_never_collide()
    {
        Assert.NotEqual(
            SettingsLocation.FileNameFor(isInstalled: true),
            SettingsLocation.FileNameFor(isInstalled: false));
    }
}
