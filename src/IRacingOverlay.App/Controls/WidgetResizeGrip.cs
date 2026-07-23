using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IRacingOverlay.Core.Settings;
// UseWindowsForms is on for the tray icon, so Point exists in both SDKs' global
// usings - see CLAUDE.md. This file means the WPF one throughout.
using Point = System.Windows.Point;

namespace IRacingOverlay.App.Controls;

/// <summary>The scale a grip drag has arrived at, and whether the drag is over.</summary>
/// <param name="Scale">The widget's new scale, already inside the supported band.</param>
/// <param name="IsFinal">
/// True on the last event of a drag. Callers use it to decide whether to announce
/// the change or just apply it: a drag produces one of these per mouse move, and
/// broadcasting every one of them re-runs the whole settings pass at frame rate.
/// </param>
public sealed class WidgetScaleEventArgs(RoutedEvent routedEvent, double scale, bool isFinal)
    : RoutedEventArgs(routedEvent)
{
    public double Scale { get; } = scale;

    public bool IsFinal { get; } = isFinal;
}

/// <summary>
/// The corner grip that resizes a widget. Sits at the bottom-right of a widget's
/// content and, dragged, scales the whole panel - it does not reflow it, because
/// the layout is the product and only its size is the user's business.
///
/// It's invisible until the pointer is over its widget: at racing speed the mouse
/// is nowhere near these windows, and a permanent handle on every panel is chrome
/// the driver never looks at. Hovering a widget is the one moment the affordance
/// is wanted, so that's when it appears (see the style in App.xaml).
///
/// The control stays ignorant of settings and widget ids: it raises
/// <see cref="ScaleChangedEvent"/>, which bubbles to the window, and the
/// composition root - which already knows which widget each window is - applies
/// and persists it. That keeps the grip a drop-in single line of XAML per widget.
/// </summary>
public sealed class WidgetResizeGrip : Thumb
{
    /// <summary>Bubbling event carrying each new scale during (and at the end of)
    /// a grip drag.</summary>
    public static readonly RoutedEvent ScaleChangedEvent = EventManager.RegisterRoutedEvent(
        "ScaleChanged",
        RoutingStrategy.Bubble,
        typeof(EventHandler<WidgetScaleEventArgs>),
        typeof(WidgetResizeGrip));

    private Window? _window;
    private FrameworkElement? _content;
    private Point _origin;
    private double _startScale = 1.0;
    private double _lastRaised = double.NaN;

    public WidgetResizeGrip()
    {
        DragStarted += OnDragStarted;
        DragDelta += OnDragDelta;
        DragCompleted += OnDragCompleted;
    }

    public event EventHandler<WidgetScaleEventArgs> ScaleChanged
    {
        add => AddHandler(ScaleChangedEvent, value);
        remove => RemoveHandler(ScaleChangedEvent, value);
    }

    private void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        _window = Window.GetWindow(this);
        _content = _window?.Content as FrameworkElement;

        // Whatever the composition root last applied - the widget's own override if
        // it has one, otherwise the shared UI scale.
        _startScale = (_content?.LayoutTransform as ScaleTransform)?.ScaleX ?? 1.0;
        _origin = PointerPosition();
        _lastRaised = double.NaN;
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs e) => Apply(isFinal: false);

    private void OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        Apply(isFinal: true);
        _window = null;
        _content = null;
    }

    private void Apply(bool isFinal)
    {
        if (_content is null)
        {
            return;
        }

        var pointer = PointerPosition();

        // Deliberately not Thumb's own HorizontalChange/VerticalChange. Those are
        // measured in the grip's coordinate space, which is inside the transform
        // being changed and moves with the corner as the widget grows - so they
        // report against a frame that shifts underneath them. The window's top-left
        // is nailed down while it resizes (it grows right and down), so measuring
        // the pointer against the window gives a fixed origin and Core can treat the
        // whole drag as one absolute offset.
        var scale = ScaleDrag.Resize(
            _startScale,
            _content.ActualWidth,
            _content.ActualHeight,
            pointer.X - _origin.X,
            pointer.Y - _origin.Y);

        // Mouse moves far outstrip the 1% steps the scale lands on; only the ones
        // that actually change something are worth raising.
        if (!isFinal && scale.Equals(_lastRaised))
        {
            return;
        }

        _lastRaised = scale;
        RaiseEvent(new WidgetScaleEventArgs(ScaleChangedEvent, scale, isFinal));
    }

    private Point PointerPosition()
        => _window is null ? new Point(0, 0) : Mouse.GetPosition(_window);
}
