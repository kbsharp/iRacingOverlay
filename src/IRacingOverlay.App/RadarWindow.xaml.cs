using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// The radar widget: borderless, transparent, always on top. A top-down
/// proximity scope that places nearby cars in the player's local frame (angles
/// and all) once the track is mapped, hides itself when nobody's near, and falls
/// back to the coarse spotter zones for the first lap. Drag it anywhere with the
/// left mouse button; right-click for the exit menu.
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
