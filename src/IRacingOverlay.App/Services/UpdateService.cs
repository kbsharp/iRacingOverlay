using System.IO;
using Velopack;
using Velopack.Sources;

namespace IRacingOverlay.App.Services;

/// <summary>
/// In-app updater built on Velopack, pointed at the public GitHub Releases feed
/// this app is published to (see <c>.github/workflows/release.yml</c>). The repo
/// is public, so no access token is needed.
///
/// It only does anything for a Velopack-<b>installed</b> copy:
/// <see cref="IsInstalled"/> is false under <c>dotnet run</c> or a portable
/// unzip, so a dev/demo launch no-ops rather than throwing.
///
/// The flow is deliberately non-intrusive for a racing overlay - check and
/// download happen silently in the background; the finished update is only
/// <em>applied</em> (which restarts the app) when the user opts in from the
/// tray, never automatically mid-session.
/// </summary>
public sealed class UpdateService
{
    // The public repo the release workflow publishes to. Public => no token.
    private const string RepoUrl = "https://github.com/kbsharp/iRacingOverlay";

    private readonly UpdateManager _manager;

    public UpdateService()
    {
        _manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    }

    /// <summary>
    /// True only for a Velopack-installed copy. False under <c>dotnet run</c> or a
    /// portable unzip, where there is no install to update - callers should treat
    /// that as "nothing to do".
    /// </summary>
    public bool IsInstalled => _manager.IsInstalled;

    /// <summary>
    /// Checks the GitHub feed and, if a newer release exists, downloads it in the
    /// background. Returns the update (to read its version and to apply later)
    /// when one is downloaded and ready to install, or <c>null</c> if the app is
    /// already current or not installed.
    ///
    /// Never throws: a flaky connection or a malformed feed must not be able to
    /// take down the overlay, so failures are logged and swallowed.
    /// </summary>
    public async Task<UpdateInfo?> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
        {
            return null;
        }

        try
        {
            var update = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return null;
            }

            await _manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            return update;
        }
        catch (Exception ex)
        {
            Log($"Update check/download failed: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Installs an already-downloaded update and restarts the app. This ends the
    /// current process, so only call it once the user has chosen to update.
    /// </summary>
    public void ApplyAndRestart(UpdateInfo update)
        => _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);

    /// <summary>
    /// Breadcrumb log for update failures. There's no general logging framework
    /// yet (that's a separate task) and a silent updater is impossible to support
    /// remotely, so failures land in <c>%LocalAppData%\IRacingOverlay\update.log</c>.
    /// Logging must never itself throw.
    /// </summary>
    internal static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IRacingOverlay");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "update.log"),
                $"{DateTime.Now:s} {message}{Environment.NewLine}");
        }
        catch
        {
            // Intentionally ignored - a logging failure must not surface to the user.
        }
    }
}
