namespace IRacingOverlay.Core.Demo;

/// <summary>
/// Turns a <see cref="RacePreset"/> and a target grid size into a class-per-car
/// assignment. Pure and deterministic so the simulated source can rebuild an
/// identical field for a given (preset, count) - and so the allocation maths
/// (largest-remainder split, guaranteeing every class and the player's class a
/// place) is unit-tested away from the untested SDK-glue source.
/// </summary>
public static class DemoFieldPlanner
{
    /// <summary>
    /// Splits <paramref name="carCount"/> cars across the preset's classes in
    /// proportion to their shares, using the largest-remainder method. Every
    /// class with a positive share gets at least one car when the field is big
    /// enough to allow it; the player's class is always guaranteed a car.
    /// </summary>
    public static int[] ClassCounts(RacePreset preset, int carCount)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentOutOfRangeException.ThrowIfLessThan(carCount, 1);

        var classes = preset.Classes.Count;
        var shares = preset.ClassShares;
        var totalShare = shares.Sum();

        var counts = new int[classes];
        var remainders = new (double Remainder, int Index)[classes];
        var assigned = 0;

        for (var i = 0; i < classes; i++)
        {
            var exact = (double)carCount * shares[i] / totalShare;
            counts[i] = (int)Math.Floor(exact);
            remainders[i] = (exact - counts[i], i);
            assigned += counts[i];
        }

        // Hand out the leftover cars to the classes with the largest fractional
        // parts (ties broken by class order for determinism).
        foreach (var (_, index) in remainders
                     .OrderByDescending(r => r.Remainder)
                     .ThenBy(r => r.Index)
                     .Take(carCount - assigned))
        {
            counts[index]++;
        }

        EnsureMinimums(preset, counts, carCount);
        return counts;
    }

    /// <summary>
    /// Returns the class index for each car (length <paramref name="carCount"/>).
    /// Car 0 - the player - always sits in <see cref="RacePreset.PlayerClassIndex"/>;
    /// the remaining cars fill the per-class counts from <see cref="ClassCounts"/>.
    /// </summary>
    public static int[] PlanClassByCar(RacePreset preset, int carCount)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentOutOfRangeException.ThrowIfLessThan(carCount, 1);

        var counts = ClassCounts(preset, carCount);
        var result = new int[carCount];

        var remaining = (int[])counts.Clone();
        result[0] = preset.PlayerClassIndex;
        remaining[preset.PlayerClassIndex]--;

        var next = 1;
        for (var classIndex = 0; classIndex < counts.Length; classIndex++)
        {
            for (var n = 0; n < remaining[classIndex]; n++)
            {
                result[next++] = classIndex;
            }
        }

        return result;
    }

    /// <summary>
    /// Nudges the split so no positive-share class is left empty (and the player's
    /// class keeps a car), taking the extra cars from the largest classes. Skipped
    /// when the field is too small to seat every class.
    /// </summary>
    private static void EnsureMinimums(RacePreset preset, int[] counts, int carCount)
    {
        var positiveClasses = preset.ClassShares.Count(s => s > 0);

        for (var i = 0; i < counts.Length; i++)
        {
            var needsOne = i == preset.PlayerClassIndex
                || (preset.ClassShares[i] > 0 && carCount >= positiveClasses);

            if (!needsOne || counts[i] > 0)
            {
                continue;
            }

            var donor = LargestDonor(counts, protectedIndex: i, preset);
            if (donor < 0)
            {
                continue;
            }

            counts[donor]--;
            counts[i]++;
        }
    }

    private static int LargestDonor(int[] counts, int protectedIndex, RacePreset preset)
    {
        var best = -1;
        for (var i = 0; i < counts.Length; i++)
        {
            var floor = i == preset.PlayerClassIndex ? 2 : 1;
            if (i == protectedIndex || counts[i] < floor)
            {
                continue;
            }

            if (best < 0 || counts[i] > counts[best])
            {
                best = i;
            }
        }

        return best;
    }
}
