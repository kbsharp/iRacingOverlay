using System.IO;
using IRacingOverlay.Core.Settings;
using Velopack;
using Velopack.Sources;

namespace IRacingOverlay.App.Services;

/// <summary>How an update check ended. "Up to date" and "couldn't ask" used to be
/// the same <c>null</c>, which made the tray's manual check claim
/// <em>"You're on the latest version"</em> when it had in fact failed to reach
/// GitHub - the exact wording that hid a broken feed for three releases.</summary>
public enum UpdateCheckStatus
{
    /// <summary>Not a Velopack install (source build, portable unzip) - there is
    /// nothing to update, and no check was attempted.</summary>
    NotInstalled,

    /// <summary>The feed was reached and this copy is current.</summary>
    UpToDate,

    /// <summary>A newer release was found and downloaded, ready to install.</summary>
    Ready,

    /// <summary>The check or download failed - see <c>update.log</c>.</summary>
    Failed,
}

/// <summary>The outcome of an update check. <paramref name="Update"/> is set only
/// for <see cref="UpdateCheckStatus.Ready"/>.</summary>
public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update);

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
    /// background.
    ///
    /// Never throws: a flaky connection or a malformed feed must not be able to
    /// take down the overlay, so failures are logged and reported as
    /// <see cref="UpdateCheckStatus.Failed"/> rather than propagating.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NotInstalled, null);
        }

        try
        {
            var update = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, null);
            }

            await _manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            return new UpdateCheckResult(UpdateCheckStatus.Ready, update);
        }
        catch (Exception ex)
        {
            Log($"Update check/download failed: {ex}");
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
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
            // Always the installed folder, never the dev one: everything that
            // reaches this log runs behind UpdateManager.IsInstalled.
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsLocation.InstalledFolderName);
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
