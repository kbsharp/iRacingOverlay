namespace IRacingOverlay.Core.Delta;

/// <summary>What the delta readout is currently reporting.</summary>
public enum DeltaState
{
    /// <summary>Nothing usable: no reference lap yet, or the player is in the
    /// pits / out of the car, where a lap delta means nothing.</summary>
    None,

    /// <summary>A running delta against the reference lap.</summary>
    Live,

    /// <summary>The lap just finished, held for a few seconds so its final
    /// number can actually be read - see <see cref="DeltaCalculator"/>.</summary>
    LapComplete,
}

/// <summary>Which way the delta reads, once the deadband is applied. Drives
/// colour only; the sign lives on <see cref="DeltaReading.Seconds"/>.</summary>
public enum DeltaTone
{
    /// <summary>Within a couple of hundredths of the reference - "on it".</summary>
    Neutral,

    Faster,

    Slower,
}
