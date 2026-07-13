using System.Windows;
using System.Windows.Input;

namespace IRacingOverlay.App;

/// <summary>
/// Demo-mode-only utility panel for exercising the overlay without
/// iRacing: add/remove cars, drain/add fuel, cycle conditions.
/// </summary>
public partial class DevControlWindow : Window
{
    public DevControlWindow()
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

    private void OnCloseGlyphClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // stop the window-level drag handler from also firing
        Hide();
    }

    private void OnClosePanel(object sender, RoutedEventArgs e) => Hide();

    private void OnExit(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).RequestExit();
}
