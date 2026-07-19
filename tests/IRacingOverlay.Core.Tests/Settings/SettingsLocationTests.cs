using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.Core.Tests.Settings;

public class SettingsLocationTests
{
    [Fact]
    public void The_installed_copy_keeps_the_original_folder()
    {
        // Load-bearing: an existing user's layout lives in
        // %LocalAppData%\IRacingOverlay\settings.json, and moving either half of
        // that path would silently reset everyone's widget positions.
        Assert.Equal("IRacingOverlay", SettingsLocation.FolderNameFor(isInstalled: true));
        Assert.Equal("settings.json", SettingsLocation.FileName);
    }

    [Fact]
    public void A_source_or_portable_build_gets_its_own_folder()
    {
        Assert.Equal("IRacingOverlay.Dev", SettingsLocation.FolderNameFor(isInstalled: false));
    }

    [Fact]
    public void The_two_never_collide()
    {
        Assert.NotEqual(
            SettingsLocation.FolderNameFor(isInstalled: true),
            SettingsLocation.FolderNameFor(isInstalled: false));
    }

    [Fact]
    public void A_source_build_writes_nothing_into_the_installed_folder()
    {
        // The installed folder is Velopack's: it creates it, updates inside it,
        // and deletes it on uninstall. A dev run leaving files there means the
        // uninstaller orphans data it never knew about.
        Assert.DoesNotContain(
            SettingsLocation.InstalledFolderName,
            SettingsLocation.FolderNameFor(isInstalled: false).Split('\\'));
    }
}
