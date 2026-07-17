namespace IRacingOverlay.Core.Radar;

/// <summary>
/// The radar's per-frame output: the cars close enough to draw, plus whether the
/// track shape has been learned yet. When <see cref="MapReady"/> is false the
/// caller should fall back to the coarse spotter signal; when
/// <see cref="AnyInRange"/> is false the widget hides itself entirely.
/// </summary>
public readonly record struct RadarResult(IReadOnlyList<RadarBlip> Blips, bool MapReady)
{
    public static readonly RadarResult Empty = new([], false);

    public bool AnyInRange => Blips.Count > 0;
}
