namespace IRacingOverlay.Core.Rating;

/// <summary>How much trust to put in the projection right now.</summary>
public enum IRatingProjectionState
{
    /// <summary>Not a race, no roster, or no rating for the player — show nothing.</summary>
    Unavailable,

    /// <summary>A race, but too early for a finishing position to mean anything.</summary>
    Pending,

    /// <summary>Racing; the projection tracks the player's current position.</summary>
    Live,

    /// <summary>The player has taken the flag; the value is captured and no longer moves.</summary>
    Final,
}

/// <summary>
/// The projected iRating outcome for the player at one moment in a race.
/// </summary>
/// <param name="State">Whether <paramref name="Delta"/> is worth showing at all.</param>
/// <param name="Delta">Projected points gained (positive) or lost (negative).</param>
/// <param name="Projected">The player's iRating if the race ended as it stands.</param>
/// <param name="Current">The player's iRating at the start of the race.</param>
/// <param name="Position">The class position the projection assumes.</param>
/// <param name="FieldSize">Rated drivers in the player's class, including those who have retired.</param>
public sealed record IRatingProjection(
    IRatingProjectionState State,
    int Delta,
    int Projected,
    int Current,
    int Position,
    int FieldSize)
{
    public static readonly IRatingProjection None =
        new(IRatingProjectionState.Unavailable, 0, 0, 0, 0, 0);

    /// <summary>True when there is a number on screen — <see cref="IRatingProjectionState.Live"/> or <see cref="IRatingProjectionState.Final"/>.</summary>
    public bool HasValue => State is IRatingProjectionState.Live or IRatingProjectionState.Final;
}
