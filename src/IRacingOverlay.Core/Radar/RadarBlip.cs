namespace IRacingOverlay.Core.Radar;

/// <summary>
/// One car placed on the radar, in the player's local frame: the player sits at
/// the origin with the nose pointing forward (+Y). +X is to the player's right,
/// +Y is ahead. Distances are metres; <paramref name="RelativeAngleRad"/> is the
/// car's heading relative to the player (0 = pointing the same way, positive =
/// rotated anticlockwise, i.e. nose swung to the player's left).
/// </summary>
public readonly record struct RadarBlip(
    int CarIdx,
    double RightMeters,
    double ForwardMeters,
    double RelativeAngleRad,
    string CarNumber,
    string? ClassColorHex);
