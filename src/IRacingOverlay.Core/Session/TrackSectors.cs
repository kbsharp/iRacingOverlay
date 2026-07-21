namespace IRacingOverlay.Core.Session;

/// <summary>
/// Names a point on the lap by the timing sector it falls in - the coarse
/// "where on track" a driver already reads off their own sector times, so it
/// needs no teaching. The boundaries come from the sim's SplitTimeInfo (see
/// <see cref="SessionMetadata.SectorStartPcts"/>); with none known, every
/// position is simply unnamed rather than guessed.
/// </summary>
public static class TrackSectors
{
    /// <summary>
    /// The 1-based sector number containing <paramref name="lapDistPct"/>, or
    /// null when no boundaries are known. Sector starts are assumed sorted
    /// ascending from 0 (the sim reports them that way); the fraction is wrapped
    /// into [0, 1) first so a projected meeting point past the line still lands
    /// in the right sector.
    /// </summary>
    public static int? SectorAt(double lapDistPct, IReadOnlyList<double>? sectorStartPcts)
    {
        if (sectorStartPcts is null || sectorStartPcts.Count == 0 || !double.IsFinite(lapDistPct))
        {
            return null;
        }

        var pct = lapDistPct - Math.Floor(lapDistPct); // -> [0, 1)

        // Walk up while the position is past each sector's start; the last one it
        // clears is the sector it's in.
        var sector = 1;
        for (var i = 0; i < sectorStartPcts.Count && pct >= sectorStartPcts[i]; i++)
        {
            sector = i + 1;
        }

        return sector;
    }
}
