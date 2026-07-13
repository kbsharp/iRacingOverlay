using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// The radar widget: borderless, transparent, always on top. Lights up (and
/// pulses) a side when iRacing's own spotter signal reports a car there.
/// Drag it anywhere with the left mouse button; right-click for the exit menu.
/// </summary>
public partial class RadarWindow : Window
{
    public RadarWindow()
    {
        InitializeComponent();
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released between the event and the DragMove call.
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).RequestExit();
}
