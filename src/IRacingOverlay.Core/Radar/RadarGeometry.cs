namespace IRacingOverlay.Core.Radar;

/// <summary>
/// Pure geometry that turns a heading-by-track-position table (see
/// <see cref="TrackMap"/>) into a car's position and orientation in the
/// player's local frame.
///
/// iRacing exposes no world position or heading for other cars - only each
/// car's <c>LapDistPct</c>. But if we know the track's shape (the heading at
/// every point around the lap, learned from the player's own driving) we can
/// walk that shape from the player's position to another car's position and
/// recover where that car sits relative to us, curves and all. On a straight
/// the walk is a straight line (a car ahead sits dead ahead); through a corner
/// the walk bends, so a car ahead ends up off to the side and angled - which is
/// exactly what makes the radar read like a real top-down view.
/// </summary>
public static class RadarGeometry
{
    /// <summary>
    /// Position and heading of the car at <paramref name="otherPct"/> relative
    /// to the player at <paramref name="playerPct"/>, in the player's local
    /// frame (+X right, +Y forward, angle in radians). Integrates the track
    /// heading along the shortest way round from player to car.
    /// </summary>
    public static (double RightMeters, double ForwardMeters, double AngleRad) RelativeTo(
        TrackMap map, double playerPct, double otherPct, double trackLengthMeters)
    {
        ArgumentNullException.ThrowIfNull(map);

        var signedDeltaPct = WrapSignedPct(otherPct - playerPct);

        // One integration step per track bucket crossed keeps the walk faithful
        // to the learned shape without doing needless work for nearby cars.
        var steps = Math.Max(1, (int)Math.Round(Math.Abs(signedDeltaPct) * map.BucketCount));
        var stepPct = signedDeltaPct / steps;
        var stepMeters = stepPct * trackLengthMeters; // signed: negative when the car is behind

        double worldX = 0, worldY = 0;
        for (var k = 0; k < steps; k++)
        {
            // Sample the heading at the midpoint of each step.
            var samplePct = Normalize01(playerPct + stepPct * (k + 0.5));
            var heading = map.HeadingAt(samplePct);
            worldX += Math.Cos(heading) * stepMeters;
            worldY += Math.Sin(heading) * stepMeters;
        }

        // Rotate the world-frame displacement into the player's frame so the
        // player's nose is +Y and the reference heading cancels out.
        var playerHeading = map.HeadingAt(Normalize01(playerPct));
        var cos = Math.Cos(playerHeading);
        var sin = Math.Sin(playerHeading);

        var forward = worldX * cos + worldY * sin;
        var right = worldX * sin - worldY * cos;

        var angle = NormalizeAngle(map.HeadingAt(Normalize01(otherPct)) - playerHeading);

        return (right, forward, angle);
    }

    /// <summary>Shortest signed lap-fraction from a to b, in (-0.5, 0.5].</summary>
    internal static double WrapSignedPct(double deltaPct)
    {
        deltaPct -= Math.Floor(deltaPct); // -> [0, 1)
        if (deltaPct > 0.5)
        {
            deltaPct -= 1.0;
        }

        return deltaPct;
    }

    /// <summary>Wrap a lap fraction into [0, 1).</summary>
    internal static double Normalize01(double pct)
    {
        pct -= Math.Floor(pct);
        // Guard the rare case where floor rounding lands exactly on 1.
        return pct >= 1.0 ? 0.0 : pct;
    }

    /// <summary>Wrap an angle into (-pi, pi].</summary>
    internal static double NormalizeAngle(double radians)
    {
        radians %= 2 * Math.PI;
        if (radians > Math.PI)
        {
            radians -= 2 * Math.PI;
        }
        else if (radians <= -Math.PI)
        {
            radians += 2 * Math.PI;
        }

        return radians;
    }
}
