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
/// own file next to it.
/// </summary>
public static class SettingsLocation
{
    /// <summary>The installed app's settings file. Unchanged from before the
    /// split: an existing layout must keep loading after an update.</summary>
    public const string InstalledFileName = "settings.json";

    /// <summary>Every non-installed copy's settings file. One shared dev file
    /// rather than one per build directory, so a rebuild or a switch between
    /// Debug and Release doesn't lose the layout you just arranged.</summary>
    public const string DevFileName = "settings.dev.json";

    /// <summary>The folder both files live in, under
    /// <c>%LocalAppData%</c>. It's the Velopack install root, above the versioned
    /// <c>current\</c>, so the installed file survives auto-updates and is
    /// removed on uninstall.</summary>
    public const string FolderName = "IRacingOverlay";

    /// <summary>
    /// The settings file name for this copy.
    /// </summary>
    /// <param name="isInstalled">
    /// Whether this is a Velopack-installed copy (<c>UpdateManager.IsInstalled</c>).
    /// </param>
    public static string FileNameFor(bool isInstalled)
        => isInstalled ? InstalledFileName : DevFileName;
}
