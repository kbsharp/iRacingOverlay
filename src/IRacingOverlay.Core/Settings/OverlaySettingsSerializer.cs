using System.Text.Json;

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
            };
        }
        catch (JsonException)
        {
            // Corrupt/hand-mangled file - fall back to defaults rather than fail.
            return new OverlaySettings();
        }
    }
}
