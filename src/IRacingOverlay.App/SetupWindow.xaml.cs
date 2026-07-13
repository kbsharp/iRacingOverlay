using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// The setup widget: borderless, transparent, always on top. Flashes for the
/// first minute of a Qualify/Race session as a reminder to double-check the
/// loaded setup. Drag it anywhere with the left mouse button; right-click
/// for the exit menu.
/// </summary>
public partial class SetupWindow : Window
{
    public SetupWindow()
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
