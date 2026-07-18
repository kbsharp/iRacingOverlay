namespace IRacingOverlay.Core.Settings;

/// <summary>
/// The single rule deciding whether a widget window should be on screen right
/// now. Two independent switches feed it: the user's per-widget on/off, and
/// whether the sim is actually running.
///
/// The second one exists because the overlay can launch with Windows, and a
/// set of always-on-top widgets parked over the desktop for the rest of the day
/// is not what "start with Windows" is asking for. Widgets stay hidden until
/// iRacing connects and go away again when it closes.
/// </summary>
public static class WidgetVisibility
{
    /// <summary>
    /// Whether a widget should be visible.
    /// </summary>
    /// <param name="isEnabled">The user's per-widget toggle.</param>
    /// <param name="isSimConnected">Whether telemetry is currently connected.</param>
    /// <param name="hideWhenSimClosed">
    /// The user preference from <see cref="OverlaySettings.HideWhenSimClosed"/>.
    /// When off, connection state is ignored and only the per-widget toggle
    /// matters (the pre-existing behaviour, kept for anyone positioning widgets
    /// without the sim open).
    /// </param>
    public static bool ShouldShow(bool isEnabled, bool isSimConnected, bool hideWhenSimClosed)
        => isEnabled && (isSimConnected || !hideWhenSimClosed);
}
