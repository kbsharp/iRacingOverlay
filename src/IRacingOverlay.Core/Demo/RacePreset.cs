namespace IRacingOverlay.Core.Demo;

/// <summary>
/// A selectable demo "race type" modelled on a real iRacing series: the classes
/// on grid, how the field is split between them, which class the player sits in,
/// and a typical grid size. The simulated telemetry source rebuilds its field
/// from one of these so the widgets can be exercised against a single-make grid,
/// a single-class GT3 field, or a full multiclass IMSA grid without iRacing.
/// </summary>
public sealed record RacePreset
{
    public RacePreset(
        string name,
        IReadOnlyList<RaceClass> classes,
        IReadOnlyList<int> classShares,
        int playerClassIndex,
        int defaultCarCount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name must be non-empty.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(classShares);

        if (classes.Count == 0)
        {
            throw new ArgumentException("A preset needs at least one class.", nameof(classes));
        }

        if (classShares.Count != classes.Count)
        {
            throw new ArgumentException("classShares must have one entry per class.", nameof(classShares));
        }

        if (classShares.Any(s => s < 0) || classShares.Sum() <= 0)
        {
            throw new ArgumentException("classShares must be non-negative with a positive total.", nameof(classShares));
        }

        if (playerClassIndex < 0 || playerClassIndex >= classes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(playerClassIndex));
        }

        if (classShares[playerClassIndex] <= 0)
        {
            throw new ArgumentException("The player's class must have a positive share.", nameof(playerClassIndex));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(defaultCarCount, 1);

        Name = name;
        Classes = classes;
        ClassShares = classShares;
        PlayerClassIndex = playerClassIndex;
        DefaultCarCount = defaultCarCount;
    }

    /// <summary>Display name, shown on the dev panel (e.g. "IMSA iRacing Series").</summary>
    public string Name { get; }

    public IReadOnlyList<RaceClass> Classes { get; }

    /// <summary>Relative field weight per class, parallel to <see cref="Classes"/>.</summary>
    public IReadOnlyList<int> ClassShares { get; }

    /// <summary>Which class car 0 (the player) races in.</summary>
    public int PlayerClassIndex { get; }

    /// <summary>Typical grid size the field is rebuilt to when this preset is selected.</summary>
    public int DefaultCarCount { get; }

    public bool IsMultiClass => Classes.Count > 1;
}
