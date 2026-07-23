namespace IRacingOverlay.Core.Settings;

/// <summary>
/// Turns a corner-grip drag into a widget scale.
///
/// Widgets are resized by scaling, not by reflowing: every panel keeps its
/// designed proportions and only its size changes, which is the whole point -
/// the layout is the product, and this exists so one opinionated layout fits any
/// monitor. So a drag of the bottom-right corner has to be reduced to a single
/// number, and the two axes will rarely agree about what that number is.
///
/// <see cref="Resize"/> takes the scale that fits the drag best in the
/// least-squares sense: of all the uniform scales available, the one whose corner
/// lands closest to where the pointer actually is. Dragging along the panel's own
/// diagonal therefore tracks the pointer exactly, and dragging purely sideways on
/// a wide panel still resizes (a little less than the pointer moved), rather than
/// one axis being picked as the master and the other ignored.
///
/// The drag is measured from a <b>fixed</b> origin - the pointer offset since the
/// drag began, against the scale the widget had then - rather than accumulated
/// frame by frame. That matters at the ends of the band: drag past 200%, and an
/// accumulating version would keep banking movement it couldn't apply and need
/// all of it unwound before the widget shrank again. Measured absolutely, running
/// past the end and coming back lands exactly where it should.
/// </summary>
public static class ScaleDrag
{
    /// <summary>
    /// The scale a drag can land on, in percentage points. Fine enough that the
    /// widget tracks the pointer smoothly, coarse enough that what gets persisted
    /// is a round number a human can read in settings.json.
    /// </summary>
    private const int Precision = 2;

    /// <summary>
    /// The scale a widget should take, given where the pointer has moved to.
    /// </summary>
    /// <param name="startScale">The widget's scale when the drag began.</param>
    /// <param name="contentWidth">Unscaled width of the widget's content, in
    /// device-independent pixels - the size a 100% widget occupies.</param>
    /// <param name="contentHeight">Unscaled height of the widget's content.</param>
    /// <param name="dragX">Pointer movement since the drag began, in screen
    /// pixels: positive is right, away from the widget's fixed top-left corner.</param>
    /// <param name="dragY">Pointer movement since the drag began, downward.</param>
    /// <returns>A scale inside the supported band (see
    /// <see cref="LayoutGuard.ClampScale"/>), rounded to the nearest percent.</returns>
    public static double Resize(
        double startScale, double contentWidth, double contentHeight, double dragX, double dragY)
    {
        // A widget that hasn't been laid out yet has no size to scale against, and
        // dividing by it would produce a scale of infinity. Nothing to do but hold
        // the scale it already has.
        if (!IsPositiveFinite(contentWidth) || !IsPositiveFinite(contentHeight)
            || !double.IsFinite(dragX) || !double.IsFinite(dragY))
        {
            return LayoutGuard.ClampScale(startScale);
        }

        // At scale s the corner sits at (W*s, H*s), so moving it by (dx, dy) asks
        // for a scale change of dx/W horizontally and dy/H vertically. This is the
        // least-squares reconciliation of those two answers.
        var change = ((contentWidth * dragX) + (contentHeight * dragY))
            / ((contentWidth * contentWidth) + (contentHeight * contentHeight));

        var scale = Math.Round(LayoutGuard.ClampScale(startScale) + change, Precision);

        return LayoutGuard.ClampScale(scale);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;
}
