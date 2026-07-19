namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// Where the driver would rejoin if they pitted at the end of this lap.
///
/// Every figure here is one the driver already owns and can check a lap later:
/// a position, a car number, a gap in seconds, and the pit loss the whole thing
/// was built on. Nothing is expressed in a unit this app invented.
/// </summary>
/// <param name="ClassPosition">Projected position within the player's own class -
/// the one that decides their race.</param>
/// <param name="OverallPosition">Projected position in the whole field.</param>
/// <param name="PositionsLost">Class places given up by stopping; 0 when the
/// driver rejoins where they already are (an empty stretch of road behind).</param>
/// <param name="PitLossSeconds">The learned cost of a stop that this projection
/// spent. Shown alongside the result so the driver can see what it assumed.</param>
/// <param name="CarAheadNumber">The car the driver would come out behind, or
/// empty when they'd rejoin at the front of their class.</param>
/// <param name="GapToCarAheadSeconds">How far behind that car, in seconds.</param>
/// <param name="CarBehindNumber">The car that would be closing on them, or empty
/// when nobody is left behind them.</param>
/// <param name="GapToCarBehindSeconds">How far that car is behind, in seconds -
/// the number that says whether the out-lap has to be defended.</param>
public readonly record struct PitExitProjection(
    int ClassPosition,
    int OverallPosition,
    int PositionsLost,
    double PitLossSeconds,
    string CarAheadNumber,
    double? GapToCarAheadSeconds,
    string CarBehindNumber,
    double? GapToCarBehindSeconds)
{
    /// <summary>No projection: not a race, no learned pit loss, or no usable gaps yet.</summary>
    public static PitExitProjection None { get; } =
        new(0, 0, 0, 0, string.Empty, null, string.Empty, null);

    /// <summary>True when there is a projection worth showing.</summary>
    public bool HasProjection => ClassPosition > 0;
}
