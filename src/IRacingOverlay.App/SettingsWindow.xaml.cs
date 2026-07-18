using System.Windows;
using IRacingOverlay.App.ViewModels;

namespace IRacingOverlay.App;

/// <summary>
/// The settings window. Unlike every other window here it is a normal, chromed,
/// taskbar-visible window - see the XAML header for why.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        // The window is rebuilt each time it's opened, so its view model must let
        // go of the settings service's Changed event or every open would leave a
        // live handler behind on a service that outlives the window.
        (DataContext as SettingsViewModel)?.Detach();
        base.OnClosed(e);
    }
}
