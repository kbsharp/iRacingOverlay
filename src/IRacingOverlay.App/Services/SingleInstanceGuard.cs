using System.Threading;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Stops a second copy of the same flavour of the app from running at once.
///
/// The overlay has no taskbar entry and its widgets are borderless and topmost,
/// so a duplicate launch isn't obvious the way a duplicate normal window is: you
/// get two tray icons, two stacks of widgets drawn on top of each other, and two
/// services debounce-writing the same settings file. Double-clicking the Start
/// menu entry while it's already running was enough to cause it.
///
/// The mutex name is scoped by flavour, so the installed app and a source build
/// don't block each other - running your dev build while the installed copy is
/// open is a normal thing to want, and they no longer share a settings file
/// (see <c>SettingsLocation</c>). It's a plain local mutex rather than
/// <c>Global\</c>: two different users on the same machine each get their own
/// per-user install, and neither should shut the other out.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private Mutex? _mutex;

    private SingleInstanceGuard(Mutex mutex) => _mutex = mutex;

    /// <summary>
    /// Attempts to claim the single-instance slot for this flavour. Returns the
    /// guard when this process is the first one - keep it alive for the process
    /// lifetime and dispose it on exit - or <c>null</c> when another copy already
    /// holds it, in which case the caller should shut down without creating any
    /// UI.
    /// </summary>
    public static SingleInstanceGuard? TryAcquire(bool isInstalled)
    {
        var name = isInstalled ? "IRacingOverlay.Installed" : "IRacingOverlay.Dev";
        var mutex = new Mutex(initiallyOwned: false, name);

        try
        {
            if (!mutex.WaitOne(TimeSpan.Zero, exitContext: false))
            {
                mutex.Dispose();
                return null;
            }
        }
        catch (AbandonedMutexException)
        {
            // The previous holder died without releasing (a crash, or Task
            // Manager). The mutex is ours now and the slot is genuinely free -
            // refusing to start here would leave the app unlaunchable until
            // reboot, which is far worse than the duplicate this guards against.
        }

        return new SingleInstanceGuard(mutex);
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owning thread (shutdown from an odd context) - the handle
            // close below still frees it for the next launch.
        }

        _mutex.Dispose();
        _mutex = null;
    }
}
