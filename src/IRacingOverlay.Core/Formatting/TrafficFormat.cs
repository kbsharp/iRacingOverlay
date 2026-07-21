using System.Globalization;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Display strings for the multiclass traffic forecast. Written to read as a
/// sentence the driver can check a lap later - who, then when and where - so no
/// token has to be decoded the way a bare rate or a made-up unit would.
/// </summary>
public static class TrafficFormat
{
    /// <summary>The approaching car, e.g. "GTP #63" - the coloured lead of the
    /// strip. Empty when there is no threat.</summary>
    public static string CarLabel(in TrafficThreat threat)
    {
        if (!threat.HasThreat)
        {
            return string.Empty;
        }

        var number = threat.CarNumber.Length > 0 ? " #" + threat.CarNumber : string.Empty;
        return threat.ClassShortName + number;
    }

    /// <summary>
    /// When and where it reaches you, e.g. "next lap · sector 2". Drops the
    /// sector when the sim hasn't reported boundaries, and collapses to just the
    /// timing rather than showing an empty half. Empty when there is no threat.
    /// </summary>
    public static string Meeting(in TrafficThreat threat)
    {
        if (!threat.HasThreat)
        {
            return string.Empty;
        }

        var when = When(threat.LapsToContact);

        return threat.MeetingSector is { } sector
            ? when + " · sector " + sector.ToString(CultureInfo.InvariantCulture)
            : when;
    }

    /// <summary>
    /// A coarse countdown in whole laps - the unit the meeting point is really
    /// known to. "this lap" while it arrives before the player next crosses the
    /// line, "next lap" through the one after, then a plain lap count.
    /// </summary>
    private static string When(double lapsToContact)
    {
        if (lapsToContact < 1)
        {
            return "this lap";
        }

        if (lapsToContact < 2)
        {
            return "next lap";
        }

        var laps = (int)Math.Floor(lapsToContact);
        return "in " + laps.ToString(CultureInfo.InvariantCulture) + " laps";
    }
}
