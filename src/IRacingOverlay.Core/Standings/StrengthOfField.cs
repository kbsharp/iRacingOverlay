namespace IRacingOverlay.Core.Standings;

/// <summary>
/// iRacing's Strength of Field for a set of iRatings — the field's effective
/// average skill, weighting lower ratings more heavily than a plain mean.
/// SoF = B·ln(n / Σ 2^(−iRating/1600)), with B = 1600/ln(2).
/// </summary>
public static class StrengthOfField
{
    private static readonly double B = 1600.0 / Math.Log(2);

    public static int Compute(IEnumerable<int> iRatings)
    {
        var count = 0;
        var sum = 0.0;

        foreach (var iRating in iRatings)
        {
            if (iRating <= 0)
            {
                continue;
            }

            count++;
            sum += Math.Exp(-iRating / B);
        }

        return count > 0 && sum > 0
            ? (int)Math.Round(B * Math.Log(count / sum))
            : 0;
    }
}
