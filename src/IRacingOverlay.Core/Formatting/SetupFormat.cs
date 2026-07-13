namespace IRacingOverlay.Core.Formatting;

/// <summary>Display formatting for the loaded car setup.</summary>
public static class SetupFormat
{
    /// <summary>Strips the ".sto" file extension iRacing setup files use, or
    /// returns a placeholder when no setup name is known.</summary>
    public static string DisplayName(string? setupName)
    {
        if (string.IsNullOrWhiteSpace(setupName))
        {
            return TelemetryFormat.Placeholder;
        }

        var trimmed = setupName.Trim();

        return trimmed.EndsWith(".sto", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
