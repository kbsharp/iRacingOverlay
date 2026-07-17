using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Loads and persists the user's <see cref="OverlaySettings"/> (UI scale + per-
/// widget window positions) to <c>%LocalAppData%\IRacingOverlay\settings.json</c>.
///
/// That path sits in the Velopack install root, above the versioned
/// <c>current\</c> folder, so it survives auto-updates (which only replace
/// <c>current\</c>) and is removed on uninstall - exactly the lifetime a layout
/// preference should have.
///
/// Writes are debounced: dragging a window fires a flurry of position changes, so
/// each change resets a short timer and only the settled value is written, rather
/// than hammering the disk. A final <see cref="SaveNow"/> on exit flushes anything
/// still pending. All members are called on the UI thread.
/// </summary>
public sealed class SettingsService
{
    private readonly string _path;
    private readonly DispatcherTimer _saveTimer;
    private readonly Dictionary<string, WindowPosition> _windows;
    private OverlaySettings _current;

    public SettingsService()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IRacingOverlay",
            "settings.json");

        _current = Load();
        _windows = new Dictionary<string, WindowPosition>(_current.Windows);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    /// <summary>The settings as loaded at startup - read once to seed the initial
    /// scale and window positions.</summary>
    public OverlaySettings Current => _current;

    /// <summary>Records a widget's new position and schedules a debounced save.</summary>
    public void SetWindowPosition(string key, double left, double top)
    {
        _windows[key] = new WindowPosition(left, top);
        ScheduleSave();
    }

    /// <summary>Records a new UI scale and schedules a debounced save.</summary>
    public void SetScale(double scale)
    {
        _current = _current with { Scale = scale };
        ScheduleSave();
    }

    /// <summary>Flushes any pending change to disk immediately (e.g. on exit).</summary>
    public void SaveNow()
    {
        _saveTimer.Stop();
        _current = _current with { Windows = new Dictionary<string, WindowPosition>(_windows) };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, OverlaySettingsSerializer.Serialize(_current));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-critical: the app runs fine, the layout just won't stick. Leave
            // a breadcrumb rather than surfacing it.
            Debug.WriteLine($"Settings save failed: {ex.Message}");
        }
    }

    private OverlaySettings Load()
    {
        try
        {
            return OverlaySettingsSerializer.Deserialize(
                File.Exists(_path) ? File.ReadAllText(_path) : null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new OverlaySettings();
        }
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
