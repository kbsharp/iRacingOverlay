namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Fixed geometry of the radar canvas, shared by <see cref="RadarBlipViewModel"/>
/// (which maps metres to canvas pixels) and the XAML (via <c>x:Static</c>, so the
/// canvas and blip sizes can't drift out of sync). The player sits at the centre
/// facing up; forward is -Y, the player's right is +X.
/// </summary>
public static class RadarLayout
{
    public const double CanvasHeight = 240;
    public const double CenterY = CanvasHeight / 2;

    /// <summary>Car icons are drawn at a fixed size, only their positions scale -
    /// the usual radar convention, so cars stay legible however close together they
    /// run, and at whatever range the driver has chosen.</summary>
    public const double BlipWidth = 12;
    public const double BlipHeight = 18;

    /// <summary>Half the along-track axis, in pixels: how far from the centre a car
    /// at the limit of range is drawn, at any range (see <see cref="ScaleFor"/>).</summary>
    private const double RangePixels = CenterY - (BlipHeight / 2);

    /// <summary>
    /// Wide enough that no car can ever be clipped. A car's lateral offset is
    /// produced by walking the learned track shape, so it's bounded by the arc
    /// walked - and the calculator only admits cars within the range, so lateral
    /// offset never exceeds the range either. In pixels that bound is
    /// <see cref="RangePixels"/> whichever range is set, because the scale is
    /// derived from it. On a hairpin this is the difference between seeing the car
    /// that's tucked inside the corner and having it vanish off the side.
    /// </summary>
    public const double CanvasWidth = (2 * RangePixels) + BlipWidth;

    public const double CenterX = CanvasWidth / 2;

    /// <summary>
    /// Canvas pixels per real metre, for the range the driver has chosen to see.
    ///
    /// Derived rather than fixed, because the range is a setting (15-200 m,
    /// <c>WidgetTuning.RadarRangeMeters</c>) while the canvas is a fixed window on
    /// screen - an overlay that resized itself when you moved a slider would be
    /// worse than one that rescales. A constant scale meant the two disagreed: at a
    /// long range cars were placed past the canvas edge, and at a short one the whole
    /// field huddled into the middle while the rest of the widget stayed blank.
    /// Scaling to the range fills the along-track axis exactly at every setting,
    /// which is also what makes the lateral axis readable - the separation the danger
    /// model grades (<c>RadarDanger.LateralRangeMeters</c>) is only a few pixels wide
    /// when the scale is set for a range nobody asked for.
    ///
    /// Half a blip is held back so a car at the limit of range sits fully on the
    /// canvas rather than half over the edge.
    /// </summary>
    public static double ScaleFor(double rangeMeters)
        => rangeMeters <= 0 ? 0 : RangePixels / rangeMeters;
}
