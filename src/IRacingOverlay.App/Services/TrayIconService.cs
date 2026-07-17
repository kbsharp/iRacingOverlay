using System.Drawing;
using System.Windows.Forms;

namespace IRacingOverlay.App.Services;

/// <summary>
/// A persistent system-tray control point for the overlay. The widget
/// windows are borderless, topmost, and have no taskbar entry, so they can
/// end up hidden behind a fullscreen game or dragged off-screen with no
/// obvious way back - and closing the launching terminal was previously the
/// only way to stop the app. This gives Show/Hide for each window and the
/// one real Exit path, independent of how the app was launched.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;

    // Revealed only once an update has been downloaded and is ready to install.
    // Kept as fields so the composition root can flip it on from the UI thread.
    private readonly ToolStripMenuItem _updateItem;
    private readonly ToolStripSeparator _updateSeparator;
    private Action? _applyUpdate;

    public TrayIconService(
        System.Windows.Window standingsWindow,
        System.Windows.Window relativeWindow,
        System.Windows.Window fuelWindow,
        System.Windows.Window setupWindow,
        System.Windows.Window radarWindow,
        System.Windows.Window? devControlWindow,
        Action<double> setScale,
        Action requestExit,
        Action checkForUpdates)
    {
        var menu = new ContextMenuStrip();

        // Update controls live at the very top so a ready update is the first
        // thing seen. Both start hidden and are switched on by ShowUpdateReady.
        _updateItem = new ToolStripMenuItem("Restart to install update") { Visible = false };
        _updateItem.Click += (_, _) => _applyUpdate?.Invoke();
        _updateSeparator = new ToolStripSeparator { Visible = false };
        menu.Items.Add(_updateItem);
        menu.Items.Add(_updateSeparator);

        menu.Items.Add("Show Standings", null, (_, _) => Reveal(standingsWindow));
        menu.Items.Add("Show Relative", null, (_, _) => Reveal(relativeWindow));
        menu.Items.Add("Show Fuel", null, (_, _) => Reveal(fuelWindow));
        menu.Items.Add("Show Setup", null, (_, _) => Reveal(setupWindow));
        menu.Items.Add("Show Radar", null, (_, _) => Reveal(radarWindow));

        menu.Items.Add(new ToolStripSeparator());
        var scaleMenu = new ToolStripMenuItem("UI Scale");
        foreach (var (label, value) in new[] { ("100%", 1.0), ("125%", 1.25), ("150%", 1.5), ("175%", 1.75) })
        {
            scaleMenu.DropDownItems.Add(label, null, (_, _) => setScale(value));
        }

        menu.Items.Add(scaleMenu);

        if (devControlWindow is not null)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Dev Controls", null, (_, _) => Reveal(devControlWindow));
        }

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

        _icon.DoubleClick += (_, _) => Reveal(relativeWindow);
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

    private static void Reveal(System.Windows.Window window)
    {
        window.Show();
        window.WindowState = System.Windows.WindowState.Normal;
        window.Activate();
    }

    // Drawn at runtime so the app doesn't need a shipped .ico asset.
    private static Icon CreateIcon()
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
