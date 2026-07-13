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

    public TrayIconService(
        System.Windows.Window relativeWindow,
        System.Windows.Window fuelWindow,
        System.Windows.Window setupWindow,
        System.Windows.Window? devControlWindow,
        Action requestExit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show Relative", null, (_, _) => Reveal(relativeWindow));
        menu.Items.Add("Show Fuel", null, (_, _) => Reveal(fuelWindow));
        menu.Items.Add("Show Setup", null, (_, _) => Reveal(setupWindow));

        if (devControlWindow is not null)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Dev Controls", null, (_, _) => Reveal(devControlWindow));
        }

        menu.Items.Add(new ToolStripSeparator());
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
