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
    /// <summary>
    /// Smallest scale a widget can be taken to. The floor is a legibility limit,
    /// not a layout one: at 80% the smallest captions (11px) land under 9px, and
    /// overlay text already renders with greyscale AA rather than ClearType
    /// (<c>AllowsTransparency</c> disables it), so below this they stop being
    /// readable at a glance rather than merely being small.
    /// </summary>
    public const double MinScale = 0.8;

    /// <summary>Largest scale a widget can be taken to - headroom above the
    /// tray's 175% for a driver sitting well back from a big screen.</summary>
    public const double MaxScale = 2.0;

    /// <summary>
    /// The scales offered as one-click presets, in the tray menu and the settings
    /// window alike - one list so the two surfaces can't disagree about what's on
    /// offer. They span the whole band, but they aren't the only sizes available:
    /// a widget's corner grip lands anywhere in between, and a size arrived at that
    /// way is offered back alongside these.
    /// </summary>
    public static readonly IReadOnlyList<double> ScalePresets =
        [0.8, 0.9, 1.0, 1.25, 1.5, 1.75, 2.0];

    /// <summary>Returns <paramref name="scale"/> if it's a finite value within the
    /// supported band, otherwise 100% - so a corrupt or hand-edited file can't
    /// shrink every widget to nothing or blow them up past the screen.</summary>
    public static double SanitizeScale(double scale)
        => double.IsFinite(scale) && scale is >= MinScale and <= MaxScale ? scale : 1.0;

    /// <summary>
    /// Pulls <paramref name="scale"/> into the supported band, keeping the nearest
    /// legal value rather than resetting to 100%. This is the live-adjustment
    /// counterpart to <see cref="SanitizeScale"/>: a drag that runs past the end of
    /// the band should stop at the end of the band, where a settings <i>file</i>
    /// holding an impossible number is corrupt and gets the default instead.
    /// </summary>
    public static double ClampScale(double scale)
        => double.IsFinite(scale) ? Math.Clamp(scale, MinScale, MaxScale) : 1.0;

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
