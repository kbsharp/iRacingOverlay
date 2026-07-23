using System.Drawing;
using System.Windows.Forms;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.Services;

/// <summary>
/// A persistent system-tray control point for the overlay. The widget
/// windows are borderless, topmost, and have no taskbar entry, so they can
/// end up hidden behind a fullscreen game or dragged off-screen with no
/// obvious way back - and closing the launching terminal was previously the
/// only way to stop the app. This gives a toggle for each widget, the shared
/// scale, the settings window, and the one real Exit path.
///
/// The widget items are <b>checkboxes, not "Show" commands</b>: a menu that can
/// only reveal a widget has no answer for "I don't want the radar", and the
/// choice is persisted, so switching one off sticks across restarts.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly SettingsService _settings;

    // Kept so the menu can be re-synced when settings change somewhere else -
    // the settings window drives the same values, and a stale checkmark here
    // would be worse than no checkmark at all.
    private readonly Dictionary<string, ToolStripMenuItem> _widgetItems = [];
    private readonly List<(ToolStripMenuItem Item, double Scale)> _scaleItems = [];
    private readonly List<(ToolStripMenuItem Item, int Hz)> _refreshItems = [];
    private readonly ToolStripMenuItem _colorBlindItem;

    // Revealed only once an update has been downloaded and is ready to install.
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripSeparator _updateSeparator;
    private Action? _applyUpdate;

    public TrayIconService(
        IReadOnlyList<OverlayWidget> widgets,
        SettingsService settings,
        Action showSettings,
        Action requestExit,
        Action checkForUpdates)
    {
        _settings = settings;

        var menu = new ContextMenuStrip();

        // Update controls live at the very top so a ready update is the first
        // thing seen. Both start hidden and are switched on by ShowUpdateReady.
        _updateItem = new ToolStripMenuItem("Restart to install update") { Visible = false };
        _updateItem.Click += (_, _) => _applyUpdate?.Invoke();
        _updateSeparator = new ToolStripSeparator { Visible = false };
        menu.Items.Add(_updateItem);
        menu.Items.Add(_updateSeparator);

        foreach (var widget in widgets.Where(w => w.IsConfigurable))
        {
            var item = new ToolStripMenuItem(widget.DisplayName)
            {
                CheckOnClick = true,
                Checked = settings.Current.IsWidgetEnabled(widget.Id),
            };

            var id = widget.Id;
            item.Click += (_, _) => _settings.SetWidgetEnabled(id, item.Checked);
            _widgetItems[id] = item;
            menu.Items.Add(item);
        }

        // The demo-only dev panel keeps a plain "show" command - it isn't a
        // persisted preference, just scaffolding to reveal.
        foreach (var widget in widgets.Where(w => !w.IsConfigurable))
        {
            menu.Items.Add(new ToolStripSeparator());
            var window = widget.Window;
            menu.Items.Add(widget.DisplayName, null, (_, _) => Reveal(window));
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(BuildScaleMenu(settings.Current.Scale));
        menu.Items.Add(BuildRefreshMenu(settings.Current.TelemetryRefreshHz));

        // The flagship accessibility toggle sits in the tray, not buried in the
        // settings dialog, because being found is the whole point of a first-mover
        // colour-vision mode. CheckOnClick + Sync keep it in step with the dialog.
        _colorBlindItem = new ToolStripMenuItem("Colour-blind palette")
        {
            CheckOnClick = true,
            Checked = settings.Current.ColorBlindPalette,
        };
        _colorBlindItem.Click += (_, _) => _settings.SetColorBlindPalette(_colorBlindItem.Checked);
        menu.Items.Add(_colorBlindItem);

        menu.Items.Add("Settings...", null, (_, _) => showSettings());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Check for updates", null, (_, _) => checkForUpdates());
        menu.Items.Add("Exit", null, (_, _) => requestExit());

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "iRacing Overlay",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Double-click reveals the relative - the glance widget, and the most
        // likely one to be hunting for.
        var relative = widgets.FirstOrDefault(w => w.Id == WidgetIds.Relative);
        if (relative is not null)
        {
            _icon.DoubleClick += (_, _) =>
            {
                _settings.SetWidgetEnabled(relative.Id, true);
                Reveal(relative.Window);
            };
        }

        settings.Changed += (_, updated) => Sync(updated);
    }

    /// <summary>
    /// Reveals the "restart to install update" item and shows a balloon tip. Call
    /// on the UI thread (NotifyIcon is UI-affine). <paramref name="apply"/> runs
    /// when the user clicks the item - it restarts the app, so it's user-driven.
    /// </summary>
    public void ShowUpdateReady(string version, Action apply)
    {
        _applyUpdate = apply;
        _updateItem.Text = $"Restart to install update v{version}";
        _updateItem.Visible = true;
        _updateSeparator.Visible = true;
        Notify("Update ready", $"Version {version} downloaded. Restart from the tray icon when you're ready.");
    }

    /// <summary>Shows a passive balloon tip (e.g. the result of a manual update
    /// check). UI-thread only.</summary>
    public void Notify(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }

    private ToolStripMenuItem BuildScaleMenu(double initialScale)
    {
        var scaleMenu = new ToolStripMenuItem("UI Scale");
        foreach (var value in LayoutGuard.ScalePresets)
        {
            var label = FormattableString.Invariant($"{value * 100:0}%");

            // Radio-style: the current scale is ticked, and picking a new one moves
            // the tick. initialScale reflects the persisted choice restored at launch.
            var item = new ToolStripMenuItem(label) { Checked = IsSameScale(value, initialScale) };
            var scale = value;
            item.Click += (_, _) => _settings.SetScale(scale);
            _scaleItems.Add((item, scale));
            scaleMenu.DropDownItems.Add(item);
        }

        return scaleMenu;
    }

    private ToolStripMenuItem BuildRefreshMenu(int initialHz)
    {
        var refreshMenu = new ToolStripMenuItem("Refresh Rate");
        foreach (var hz in TelemetryRefresh.AllowedHz)
        {
            // Radio-style, like UI Scale: the active rate is ticked, and the default
            // is labelled so it's clear which one to come back to.
            var label = hz == TelemetryRefresh.DefaultHz ? $"{hz} Hz (default)" : $"{hz} Hz";
            var item = new ToolStripMenuItem(label) { Checked = hz == initialHz };
            var rate = hz;
            item.Click += (_, _) => _settings.SetTelemetryRefreshHz(rate);
            _refreshItems.Add((item, rate));
            refreshMenu.DropDownItems.Add(item);
        }

        return refreshMenu;
    }

    // Re-point every checkmark at the settings, wherever the change came from.
    private void Sync(OverlaySettings settings)
    {
        foreach (var (id, item) in _widgetItems)
        {
            item.Checked = settings.IsWidgetEnabled(id);
        }

        foreach (var (item, scale) in _scaleItems)
        {
            item.Checked = IsSameScale(scale, settings.Scale);
        }

        foreach (var (item, hz) in _refreshItems)
        {
            item.Checked = hz == settings.TelemetryRefreshHz;
        }

        _colorBlindItem.Checked = settings.ColorBlindPalette;
    }

    private static bool IsSameScale(double a, double b) => Math.Abs(a - b) < 0.001;

    private static void Reveal(System.Windows.Window window)
    {
        window.Show();
        window.WindowState = System.Windows.WindowState.Normal;
        window.Activate();
    }

    /// <summary>
    /// The shared app icon (<c>Assets/app.ico</c>), at whatever size the current
    /// tray wants - it varies with DPI, and handing <see cref="NotifyIcon"/> a
    /// fixed 32px bitmap left it resampled and soft on a scaled display. The .ico
    /// carries real 16/20/24/32px drawings, so asking for the system size picks
    /// one rather than shrinking the big one.
    ///
    /// Falls back to a drawn circle if the resource can't be loaded: an app that
    /// won't start because of an icon would be a poor trade, and the tray icon is
    /// the only way to quit.
    /// </summary>
    private static Icon CreateIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream is not null)
            {
                return new Icon(stream, SystemInformation.SmallIconSize);
            }
        }
        catch (Exception ex)
        {
            UpdateService.Log($"Tray icon resource failed to load, using fallback: {ex}");
        }

        return CreateFallbackIcon();
    }

    private static Icon CreateFallbackIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            using var background = new SolidBrush(ColorTranslator.FromHtml("#141C2A"));
            g.FillEllipse(background, 0, 0, 32, 32);

            using var accent = new SolidBrush(ColorTranslator.FromHtml("#39A7FF"));
            g.FillEllipse(accent, 9, 9, 14, 14);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
