namespace IRacingOverlay.Core.Relative;

/// <summary>How a car's race progress compares to the player's, in whole laps.</summary>
public enum LapDifference
{
    SameLap,
    LapAhead,
    LapBehind,
}

/// <summary>One display row of the relative widget.</summary>
public sealed record RelativeRow(
    int CarIdx,
    bool IsPlayer,
    int Position,
    string CarNumber,
    string DisplayName,
    string License,
    int IRating,
    double DeltaSeconds,
    LapDifference LapDifference,
    bool InPits);
