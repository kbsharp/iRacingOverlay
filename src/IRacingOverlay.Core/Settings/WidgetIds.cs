namespace IRacingOverlay.Core.Settings;

/// <summary>
/// The stable per-widget keys used throughout <see cref="OverlaySettings"/>.
///
/// The values are the App's window type names, which is what the original
/// layout-persistence code keyed <see cref="OverlaySettings.Windows"/> by. They
/// are kept verbatim so an existing <c>settings.json</c> still restores its saved
/// positions - renaming a value here silently resets every user's layout.
/// </summary>
public static class WidgetIds
{
    public const string Standings = "StandingsWindow";
    public const string Relative = "RelativeWindow";
    public const string Fuel = "FuelWindow";
    public const string Radar = "RadarWindow";
    public const string Delta = "DeltaWindow";
    public const string TrackMap = "TrackMapWindow";

    /// <summary>Every widget the settings surface knows about, in the order the
    /// tray menu and settings window list them.
    ///
    /// There was a "SetupWindow" entry here until the setup readout was folded
    /// into the fuel widget. A settings file written before that still carries
    /// its keys; every per-widget map is read by lookup, so the stale entries are
    /// simply never consulted.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Standings, Relative, Fuel, Radar, Delta, TrackMap];
}
