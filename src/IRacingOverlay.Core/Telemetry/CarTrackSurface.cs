namespace IRacingOverlay.Core.Telemetry;

/// <summary>Mirrors iRacing's CarIdxTrackSurface values.</summary>
public enum CarTrackSurface
{
    NotInWorld = -1,
    OffTrack = 0,
    InPitStall = 1,
    ApproachingPits = 2,
    OnTrack = 3,
}
