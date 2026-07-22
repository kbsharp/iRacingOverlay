namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Fixed geometry of the track-map canvas, shared by <see cref="TrackMapViewModel"/>
/// (which maps the normalised outline onto it) and the XAML (via <c>x:Static</c>,
/// so the canvas and the marks can't drift out of sync).
///
/// Square, because the outline is scaled uniformly into a unit box - a rectangle
/// would only add blank space on one axis for every circuit that doesn't happen
/// to match its proportions.
/// </summary>
public static class TrackMapLayout
{
    public const double CanvasSize = 208;

    /// <summary>Car marks are a fixed size, only their positions scale - the same
    /// convention the radar uses, so cars stay legible wherever the field is
    /// bunched up.</summary>
    public const double CarSize = 9;

    /// <summary>Your own mark is a size up, and carries a ring the others don't:
    /// on a map of twenty identical dots, finding yourself must be instant.</summary>
    public const double PlayerCarSize = 13;

    /// <summary>How wide the track itself is drawn. Wide enough to read as a
    /// circuit rather than a wire, and to sit a car mark on.</summary>
    public const double TrackThickness = 7;

    /// <summary>The dark edge drawn under the tarmac, so the ribbon has an outline
    /// where it runs close to itself or under a car.</summary>
    public const double CasingThickness = TrackThickness + 3;

    /// <summary>Room kept clear at the edges so a car sitting on the outermost
    /// point of the lap is drawn whole rather than clipped by the canvas.</summary>
    private const double Margin = (PlayerCarSize / 2) + (CasingThickness / 2);

    /// <summary>The side of the square the outline itself is drawn into.</summary>
    public const double DrawSize = CanvasSize - (2 * Margin);

    /// <summary>Turns a normalised outline coordinate (0-1) into a canvas pixel.</summary>
    public static double ToCanvas(double normalized) => Margin + (normalized * DrawSize);
}
