using System.Globalization;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Formatting;

/// <summary>
/// Display strings for the pit-exit projection.
///
/// Written against the lesson the safety chip and the catch/defend column both
/// taught: a figure has to carry its unit and its referent on the row. "P4" on
/// its own is the same failure as a bare "▲ 0.2" - a number sat among other
/// numbers, waiting to be decoded. So the projection reads as a sentence the
/// driver can check a lap later: how far behind whom, how much room to whoever
/// is coming, and what the stop was assumed to cost.
/// </summary>
public static class PitExitFormat
{
    /// <summary>The projected class position, e.g. "P4".</summary>
    public static string Position(in PitExitProjection projection) =>
        projection.HasProjection
            ? "P" + projection.ClassPosition.ToString(CultureInfo.InvariantCulture)
            : TelemetryFormat.Placeholder;

    /// <summary>
    /// Places the stop gives up, e.g. "▼2" - empty when it costs nothing, because
    /// "▼0" is a figure to read and dismiss rather than one that says anything.
    /// </summary>
    public static string PositionsLost(in PitExitProjection projection) =>
        projection.HasProjection && projection.PositionsLost > 0
            ? "▼" + projection.PositionsLost.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    /// <summary>
    /// The pit loss the projection spent, e.g. "costs 29s". Shown so the driver can
    /// see the one learned number the whole answer is scaled by - without it the
    /// projection is unfalsifiable, which is exactly what sank the safety chip.
    /// </summary>
    public static string Cost(in PitExitProjection projection) =>
        projection.HasProjection
            ? "costs " + Seconds(projection.PitLossSeconds, decimals: 0) + "s"
            : string.Empty;

    /// <summary>
    /// Who the driver would land between, as a sentence:
    /// "5.0s behind #12 · 8.0s clear of #7".
    /// </summary>
    public static string Neighbours(in PitExitProjection projection)
    {
        if (!projection.HasProjection)
        {
            return string.Empty;
        }

        var ahead = Ahead(projection);
        var behind = Behind(projection);

        if (ahead.Length == 0)
        {
            return behind.Length == 0 ? "clear track" : behind;
        }

        return behind.Length == 0 ? ahead : ahead + " · " + behind;
    }

    private static string Ahead(in PitExitProjection projection)
    {
        if (projection.GapToCarAheadSeconds is not { } gap || projection.CarAheadNumber.Length == 0)
        {
            // Rejoining at the front of your own class is worth saying outright.
            return projection.ClassPosition == 1 ? "still leads the class" : string.Empty;
        }

        return Seconds(gap) + "s behind #" + projection.CarAheadNumber;
    }

    private static string Behind(in PitExitProjection projection)
    {
        if (projection.GapToCarBehindSeconds is not { } gap || projection.CarBehindNumber.Length == 0)
        {
            return string.Empty;
        }

        return Seconds(gap) + "s clear of #" + projection.CarBehindNumber;
    }

    private static string Seconds(double value, int decimals = 1) =>
        value.ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);
}
