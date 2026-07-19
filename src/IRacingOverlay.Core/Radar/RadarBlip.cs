namespace IRacingOverlay.Core.Radar;

/// <summary>
/// One car placed on the radar, in the player's local frame: the player sits at
/// the origin with the nose pointing forward (+Y). +X is to the player's right,
/// +Y is ahead. Distances are metres; <paramref name="RelativeAngleRad"/> is the
/// car's heading relative to the player (0 = pointing the same way, positive =
/// rotated anticlockwise, i.e. nose swung to the player's left).
///
/// <paramref name="LateralUnresolved"/> marks the one case the geometry cannot
/// answer: a car level with us. Lateral offset here is derived from along-track
/// offset walked through the track's shape, so two cars genuinely door-to-door
/// have almost the same lap fraction and both land on the centreline whatever
/// lane they are really in. When the sim's own spotter can name the side we take
/// its word (see <see cref="RadarCalculator"/>); when it can't, this flag says so
/// rather than letting a guess be drawn as confidently as a measurement.
/// </summary>
public readonly record struct RadarBlip(
    int CarIdx,
    double RightMeters,
    double ForwardMeters,
    double RelativeAngleRad,
    string CarNumber,
    string? ClassColorHex,
    bool LateralUnresolved = false);
