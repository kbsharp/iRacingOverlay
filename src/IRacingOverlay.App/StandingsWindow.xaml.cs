using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// The standings widget: the full, class-grouped field. Borderless,
/// transparent, always on top. Drag it anywhere with the left mouse button;
/// right-click for the exit menu.
/// </summary>
public partial class StandingsWindow : Window
{
    public StandingsWindow()
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
