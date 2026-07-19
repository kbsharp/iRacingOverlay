using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using IRacingOverlay.Core.Rating;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Loads and persists the user's <see cref="OverlaySettings"/> (UI scale + per-
/// widget window positions) to <c>%LocalAppData%\IRacingOverlay\</c>.
///
/// That path sits in the Velopack install root, above the versioned
/// <c>current\</c> folder, so it survives auto-updates (which only replace
/// <c>current\</c>) and is removed on uninstall - exactly the lifetime a layout
/// preference should have.
///
/// The file name depends on whether this is the installed copy - see
/// <see cref="SettingsLocation"/>. A source build must not write over the layout
/// the user arranged for real racing.
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

    /// <param name="isInstalled">Whether this is the Velopack-installed copy
    /// (<c>UpdateService.IsInstalled</c>). Decides which file is used.</param>
    public SettingsService(bool isInstalled)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        _path = Path.Combine(
            localAppData,
            SettingsLocation.FolderNameFor(isInstalled),
            SettingsLocation.FileName);

        if (!isInstalled)
        {
            MigrateLegacyDevSettings(localAppData, _path);
        }

        _current = Load();
        _windows = new Dictionary<string, WindowPosition>(_current.Windows);

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _saveTimer.Tick += (_, _) => SaveNow();
    }

    /// <summary>Raised after any change, with the new settings, so the app can
    /// re-apply scale/visibility/units without each caller having to.</summary>
    public event EventHandler<OverlaySettings>? Changed;

    /// <summary>The current settings - seeded from disk at startup and updated
    /// in place by every setter here.</summary>
    public OverlaySettings Current => _current;

    /// <summary>Records a widget's new position and schedules a debounced save.
    /// Unlike the other setters this does <b>not</b> raise <see cref="Changed"/>:
    /// it fires continuously while a window is dragged, and nothing needs to
    /// react to a position but the disk.</summary>
    public void SetWindowPosition(string key, double left, double top)
    {
        _windows[key] = new WindowPosition(left, top);
        ScheduleSave();
    }

    /// <summary>
    /// Records the driver's rolling corners-per-incident baseline after a
    /// session is banked. Like <see cref="SetWindowPosition"/> this does
    /// <b>not</b> raise <see cref="Changed"/> - nothing in the UI is configured
    /// by it, and re-applying every setting mid-session to store a number that
    /// only the safety chip reads would be a lot of work for no effect.
    /// </summary>
    public void SetSafetyHistory(CpiHistory history)
    {
        _current = _current with { SafetyHistory = history };
        ScheduleSave();
    }

    /// <summary>Records a new shared UI scale and schedules a debounced save.</summary>
    public void SetScale(double scale)
        => Update(_current with { Scale = scale });

    /// <summary>Turns a widget on or off.</summary>
    public void SetWidgetEnabled(string widgetId, bool enabled)
        => Update(_current with { EnabledWidgets = With(_current.EnabledWidgets, widgetId, enabled) });

    /// <summary>Overrides one widget's scale, or clears the override (null) so it
    /// follows the shared scale again.</summary>
    public void SetWidgetScale(string widgetId, double? scale)
    {
        var scales = new Dictionary<string, double>(_current.WidgetScales);
        if (scale is { } value)
        {
            scales[widgetId] = value;
        }
        else
        {
            scales.Remove(widgetId);
        }

        Update(_current with { WidgetScales = scales });
    }

    /// <summary>Makes a widget transparent to the mouse, or interactive again.</summary>
    public void SetClickThrough(string widgetId, bool clickThrough)
        => Update(_current with { ClickThroughWidgets = With(_current.ClickThroughWidgets, widgetId, clickThrough) });

    /// <summary>Replaces the display units.</summary>
    public void SetUnits(UnitPreferences units)
        => Update(_current with { Units = units.Sanitized() });

    /// <summary>Replaces the per-widget tuning numbers.</summary>
    public void SetTuning(WidgetTuning tuning)
        => Update(_current with { Tuning = tuning.Sanitized() });

    /// <summary>Records whether the app launches with Windows. Store the value the
    /// registry write actually achieved, not the one requested.</summary>
    public void SetRunAtStartup(bool runAtStartup)
        => Update(_current with { RunAtStartup = runAtStartup });

    /// <summary>Records whether widgets hide while iRacing isn't running.</summary>
    public void SetHideWhenSimClosed(bool hideWhenSimClosed)
        => Update(_current with { HideWhenSimClosed = hideWhenSimClosed });

    /// <summary>Records whether the fuel widget carries the setup strip and its
    /// start-of-session pulse.</summary>
    public void SetShowSetupReminder(bool showSetupReminder)
        => Update(_current with { ShowSetupReminder = showSetupReminder });

    /// <summary>Records whether the standings shows the manufacturer badge column.</summary>
    public void SetShowManufacturerBadges(bool showManufacturerBadges)
        => Update(_current with { ShowManufacturerBadges = showManufacturerBadges });

    /// <summary>Forgets every saved window position, so the next launch puts each
    /// widget back at its default corner. The recovery path for a layout that's
    /// been dragged somewhere unusable - previously this meant deleting
    /// settings.json by hand.</summary>
    public void ResetLayout()
    {
        _windows.Clear();
        Update(_current with { Windows = new Dictionary<string, WindowPosition>() });
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

    /// <summary>
    /// Moves a dev layout written before source builds got their own folder
    /// (<c>IRacingOverlay\settings.dev.json</c>) to its new home. Best-effort and
    /// one-way: it only ever runs when the new file doesn't exist yet, so it
    /// can't clobber a newer layout, and any failure just means starting from
    /// defaults - which is what would have happened without the migration.
    /// Copies rather than moves, deliberately: the source lives inside the
    /// installed app's folder, and this build has no business deleting from
    /// there.
    /// </summary>
    private static void MigrateLegacyDevSettings(string localAppData, string newPath)
    {
        try
        {
            if (File.Exists(newPath))
            {
                return;
            }

            var legacy = Path.Combine(
                localAppData,
                SettingsLocation.InstalledFolderName,
                SettingsLocation.LegacyDevFileName);

            if (!File.Exists(legacy))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Copy(legacy, newPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dev settings migration failed: {ex.Message}");
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

    // Every non-position setter funnels through here so there is one place that
    // both schedules the save and announces the change.
    private void Update(OverlaySettings settings)
    {
        _current = settings;
        ScheduleSave();
        Changed?.Invoke(this, _current);
    }

    private static Dictionary<string, T> With<T>(
        IReadOnlyDictionary<string, T> source, string key, T value)
        => new(source) { [key] = value };
}
