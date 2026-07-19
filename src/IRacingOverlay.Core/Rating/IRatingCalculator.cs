namespace IRacingOverlay.Core.Rating;

/// <summary>
/// iRacing's iRating change, as an Elo-style pairwise model.
///
/// Each driver's race performance is modelled as a draw from an exponential
/// distribution whose scale comes from their rating, giving a closed-form
/// probability that i finishes ahead of j:
///
///   f(R) = 2^(-R/1600)
///   P(i beats j) = (1 - f_i)·f_j / (f_i + f_j - 2·f_i·f_j)
///
/// Summing that over the field gives the number of drivers i was *expected* to
/// beat; the difference against how many they actually beat drives the change.
/// Because both sums equal n(n-1)/2 over the whole field, the changes are
/// zero-sum — rating is transferred between drivers, never created, which is
/// the property that makes the projection believable rather than merely
/// plausible.
///
/// This is the community-reconstructed model, not iRacing's shipped code: the
/// shape is right and the scale is calibrated, but treat the output as an
/// estimate of a few points, not a guarantee.
/// </summary>
public static class IRatingCalculator
{
    /// <summary>
    /// Scales the "drivers beaten" surplus into rating points. Chosen so a win
    /// against an evenly matched full field lands in iRacing's usual range
    /// (~+95 for winning a 20-car race at your own strength).
    /// </summary>
    private const double PointsPerDriver = 200.0;

    /// <summary>The rating interval over which the odds of winning halve.</summary>
    private const double RatingScale = 1600.0;

    /// <summary>
    /// The projected change for the driver at <paramref name="index"/> finishing
    /// at <paramref name="finishPosition"/> (1-based) in a field of
    /// <paramref name="ratings"/>. Returns 0 when the field is too small or the
    /// inputs are out of range.
    /// </summary>
    public static int Delta(IReadOnlyList<int> ratings, int index, int finishPosition)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var n = ratings.Count;

        if (n < 2 || index < 0 || index >= n || finishPosition < 1 || finishPosition > n)
        {
            return 0;
        }

        var expectedBeaten = ExpectedBeaten(ratings, index);
        var actuallyBeaten = n - finishPosition;

        return (int)Math.Round(PointsPerDriver * (actuallyBeaten - expectedBeaten) / n);
    }

    /// <summary>
    /// How many of the other drivers this one is expected to finish ahead of.
    /// Exposed for tests and for anyone wanting the "expected finish" directly
    /// (which is <c>count - 1 - ExpectedBeaten + 1</c>).
    /// </summary>
    public static double ExpectedBeaten(IReadOnlyList<int> ratings, int index)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        if (index < 0 || index >= ratings.Count)
        {
            return 0;
        }

        var self = Strength(ratings[index]);
        var expected = 0.0;

        for (var j = 0; j < ratings.Count; j++)
        {
            if (j == index)
            {
                continue;
            }

            expected += BeatProbability(self, Strength(ratings[j]));
        }

        return expected;
    }

    /// <summary>Probability the driver with strength <paramref name="self"/> finishes ahead of <paramref name="other"/>.</summary>
    private static double BeatProbability(double self, double other)
    {
        var denominator = self + other - (2 * self * other);

        // Two drivers at the rating floor collapse the denominator; call it even.
        return denominator > 0 ? (1 - self) * other / denominator : 0.5;
    }

    private static double Strength(int rating) => Math.Pow(2, -Math.Max(rating, 1) / RatingScale);
}
