using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace IRacingOverlay.App.Services;

/// <summary>
/// Registers (or unregisters) the app in the per-user Run key so it launches with
/// Windows. Per-user (HKCU) deliberately: it needs no elevation, and matches
/// where Velopack installs the app in the first place.
///
/// The registered path is <see cref="Environment.ProcessPath"/>, which under
/// Velopack is the stub launcher above the versioned <c>current\</c> folder - so
/// the entry keeps working across auto-updates rather than pinning a version that
/// later disappears.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IRacingOverlay";

    /// <summary>True if the Run entry is currently present.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Adds or removes the Run entry. Returns the state actually achieved, which
    /// the caller should persist rather than the state requested - a locked-down
    /// machine can refuse the write, and the settings file shouldn't then claim a
    /// startup entry exists when it doesn't.
    /// </summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
            {
                return IsEnabled();
            }

            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }

                key.SetValue(ValueName, $"\"{path}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return enabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Non-critical: the app runs fine, it just won't auto-start. Report
            // what's actually true rather than what was asked for.
            Debug.WriteLine($"Startup registration failed: {ex.Message}");
            return IsEnabled();
        }
    }
}
