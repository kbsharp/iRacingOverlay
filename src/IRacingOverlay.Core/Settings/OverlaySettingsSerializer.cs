using System.Text.Json;
using IRacingOverlay.Core.Rating;

namespace IRacingOverlay.Core.Settings;

/// <summary>
/// JSON (de)serialization for <see cref="OverlaySettings"/>. Deserialization is
/// deliberately forgiving: a missing, empty, or corrupt settings file must never
/// throw or take down startup - it just yields defaults - and an out-of-range
/// scale is sanitized (see <see cref="LayoutGuard.SanitizeScale"/>). Positions
/// are validated for the current display setup separately, at apply time.
/// </summary>
public static class OverlaySettingsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(OverlaySettings settings)
        => JsonSerializer.Serialize(settings, Options);

    public static OverlaySettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OverlaySettings();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<OverlaySettings>(json, Options);
            if (parsed is null)
            {
                return new OverlaySettings();
            }

            return parsed with
            {
                Scale = LayoutGuard.SanitizeScale(parsed.Scale),
                Windows = parsed.Windows ?? new Dictionary<string, WindowPosition>(),
                EnabledWidgets = parsed.EnabledWidgets ?? new Dictionary<string, bool>(),
                ClickThroughWidgets = parsed.ClickThroughWidgets ?? new Dictionary<string, bool>(),
                // Per-widget scales go through the same band as the global one, so
                // a hand-edited 0.01 can't shrink one widget out of existence.
                WidgetScales = SanitizeScales(parsed.WidgetScales),
                Units = (parsed.Units ?? new UnitPreferences()).Sanitized(),
                Tuning = (parsed.Tuning ?? new WidgetTuning()).Sanitized(),
                SafetyHistory = SanitizeSafetyHistory(parsed.SafetyHistory),
            };
        }
        catch (JsonException)
        {
            // Corrupt/hand-mangled file - fall back to defaults rather than fail.
            return new OverlaySettings();
        }
    }

    /// <summary>
    /// The safety baseline is accumulated data rather than a preference, so a
    /// nonsensical value (negative, NaN, or a corner count beyond the window it
    /// is meant to cover) means the file was hand-edited or written by a
    /// different build. Start it over rather than judging real sessions against
    /// a figure that can't have been earned.
    /// </summary>
    private static CpiHistory SanitizeSafetyHistory(CpiHistory? history)
    {
        if (history is null
            || !double.IsFinite(history.Corners) || !double.IsFinite(history.IncidentPoints)
            || history.Corners < 0 || history.IncidentPoints < 0
            || history.Corners > CpiHistory.WindowCorners)
        {
            return CpiHistory.Empty;
        }

        return history;
    }

    private static IReadOnlyDictionary<string, double> SanitizeScales(
        IReadOnlyDictionary<string, double>? scales)
    {
        if (scales is null || scales.Count == 0)
        {
            return new Dictionary<string, double>();
        }

        return scales.ToDictionary(pair => pair.Key, pair => LayoutGuard.SanitizeScale(pair.Value));
    }
}
