namespace IRacingOverlay.Core.Settings;

/// <summary>A saved window position (top-left corner, in device-independent
/// pixels), keyed per widget in <see cref="OverlaySettings.Windows"/>.</summary>
public sealed record WindowPosition(double Left, double Top);

/// <summary>
/// Persisted user layout for the overlay: the shared UI scale and each widget's
/// on-screen position, so the app comes back the way it was left rather than
/// resetting to the default corners every launch. Serialized to JSON by
/// <see cref="OverlaySettingsSerializer"/>; loaded/saved by the app's
/// SettingsService.
/// </summary>
public sealed record OverlaySettings
{
    /// <summary>Shared UI scale applied to every widget (1.0 = 100%).</summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>Window position by widget key (the window's type name). Widgets
    /// with no saved entry fall back to their default XAML position.</summary>
    public IReadOnlyDictionary<string, WindowPosition> Windows { get; init; }
        = new Dictionary<string, WindowPosition>();
}
