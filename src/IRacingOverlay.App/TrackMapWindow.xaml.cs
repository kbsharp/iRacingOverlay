using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// The track-map widget: borderless, transparent, always on top. Draws the
/// circuit as learned from the player's own driving, with the whole field placed
/// on it by lap fraction. Drag it anywhere with the left mouse button;
/// right-click for the exit menu.
/// </summary>
public partial class TrackMapWindow : Window
{
    public TrackMapWindow()
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
