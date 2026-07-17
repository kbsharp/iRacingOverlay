namespace IRacingOverlay.Core.Settings;

/// <summary>A rectangle in screen coordinates - used to describe the virtual
/// desktop (all monitors) when validating a saved window position.</summary>
public sealed record LayoutBounds(double Left, double Top, double Width, double Height);

/// <summary>
/// Pure guards that keep a restored layout sane. A settings file can outlive the
/// hardware it was written on - a monitor gets unplugged, a resolution changes -
/// so restored values are validated before being trusted, rather than stranding
/// a widget off-screen or applying a nonsense scale.
/// </summary>
public static class LayoutGuard
{
    // The tray offers 100/125/150/175%; allow a little headroom above and clamp
    // anything outside a sane band (or non-finite) back to 100%.
    private const double MinScale = 1.0;
    private const double MaxScale = 2.0;

    /// <summary>Returns <paramref name="scale"/> if it's a finite value within the
    /// supported band, otherwise 100% - so a corrupt or hand-edited file can't
    /// shrink every widget to nothing or blow them up past the screen.</summary>
    public static double SanitizeScale(double scale)
        => double.IsFinite(scale) && scale is >= MinScale and <= MaxScale ? scale : 1.0;

    /// <summary>
    /// True if the window's top-left corner lands on the virtual desktop (with a
    /// small margin so it stays grabbable). A position saved on a monitor that's
    /// since been removed fails this, and the caller falls back to the widget's
    /// default position instead of restoring it somewhere invisible.
    /// </summary>
    public static bool IsOnScreen(WindowPosition position, LayoutBounds virtualScreen, double margin = 8)
    {
        if (!double.IsFinite(position.Left) || !double.IsFinite(position.Top))
        {
            return false;
        }

        var right = virtualScreen.Left + virtualScreen.Width;
        var bottom = virtualScreen.Top + virtualScreen.Height;

        return position.Left >= virtualScreen.Left - margin
            && position.Top >= virtualScreen.Top - margin
            && position.Left <= right - margin
            && position.Top <= bottom - margin;
    }
}
