namespace IRacingOverlay.Core.Settings;

/// <summary>
/// Which settings file a copy of the app owns.
///
/// The installed app and a source build both run as the same user on the same
/// machine, so a single fixed path means a <c>dotnet run</c> session loads the
/// real layout and writes back wherever the dev windows happened to land -
/// silently overwriting positions the user dragged for real racing. Worse when
/// both are open at once: each debounce-saves the whole file, so the last writer
/// wins and one copy's widget toggles vanish.
///
/// Splitting them by install kind keeps the installed app's file exactly where it
/// was (so existing layouts survive this change) and gives every other copy -
/// <c>dotnet run</c>, a portable unzip, a build straight out of <c>bin\</c> - its
/// own file in its own folder.
/// </summary>
public static class SettingsLocation
{
    /// <summary>The settings file name, now the same for both copies because the
    /// <em>folder</em> is what separates them.</summary>
    public const string FileName = "settings.json";

    /// <summary>The installed app's folder under <c>%LocalAppData%</c>. It's the
    /// Velopack install root, above the versioned <c>current\</c>, so the
    /// installed file survives auto-updates and is removed on uninstall.
    /// Unchanged from before the split: an existing layout must keep loading
    /// after an update.</summary>
    public const string InstalledFolderName = "IRacingOverlay";

    /// <summary>Every non-installed copy's folder. One shared dev folder rather
    /// than one per build directory, so a rebuild or a switch between Debug and
    /// Release doesn't lose the layout you just arranged.</summary>
    public const string DevFolderName = "IRacingOverlay.Dev";

    /// <summary>The dev settings file as it was named when both copies shared the
    /// installed folder. Only still referenced so an existing dev layout can be
    /// migrated across once - see <c>SettingsService</c>.</summary>
    public const string LegacyDevFileName = "settings.dev.json";

    /// <summary>
    /// The folder this copy owns. Separate folders rather than two file names in
    /// one folder: a source build should leave no trace inside the installed
    /// app's directory, which is Velopack's to create, update and delete on
    /// uninstall. Sharing it meant a dev run left files behind that the
    /// uninstaller had no reason to know about.
    /// </summary>
    /// <param name="isInstalled">
    /// Whether this is a Velopack-installed copy (<c>UpdateManager.IsInstalled</c>).
    /// </param>
    public static string FolderNameFor(bool isInstalled)
        => isInstalled ? InstalledFolderName : DevFolderName;
}
