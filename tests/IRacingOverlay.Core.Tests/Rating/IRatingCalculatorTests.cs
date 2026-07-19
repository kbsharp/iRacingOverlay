using IRacingOverlay.Core.Rating;

namespace IRacingOverlay.Core.Tests.Rating;

public class IRatingCalculatorTests
{
    private static int[] Uniform(int count, int rating) => [.. Enumerable.Repeat(rating, count)];

    [Fact]
    public void Delta_WinningAnEvenField_GainsRoughlyHalfTheFieldInPoints()
    {
        // 20 evenly matched drivers: the winner beat 19 but was only expected to
        // beat 9.5, so the surplus is half the field.
        var delta = IRatingCalculator.Delta(Uniform(20, 2000), index: 0, finishPosition: 1);

        Assert.InRange(delta, 90, 100);
    }

    [Fact]
    public void Delta_LastInAnEvenField_MirrorsTheWin()
    {
        var field = Uniform(20, 2000);

        var won = IRatingCalculator.Delta(field, index: 0, finishPosition: 1);
        var lost = IRatingCalculator.Delta(field, index: 0, finishPosition: 20);

        Assert.Equal(won, -lost);
    }

    [Fact]
    public void Delta_FinishingExactlyAsExpected_IsAboutZero()
    {
        // In an even field of 21, the expected finish is the middle of the grid.
        var delta = IRatingCalculator.Delta(Uniform(21, 2000), index: 0, finishPosition: 11);

        Assert.InRange(delta, -2, 2);
    }

    [Fact]
    public void Delta_AcrossTheWholeField_SumsToZero()
    {
        // iRating is transferred, never minted. A field where everyone finishes
        // in a distinct position must net out to nothing.
        int[] field = [1200, 1800, 2400, 3000, 4500, 900];

        var total = 0;
        for (var i = 0; i < field.Length; i++)
        {
            total += IRatingCalculator.Delta(field, i, finishPosition: i + 1);
        }

        // Only integer rounding should survive.
        Assert.InRange(total, -field.Length, field.Length);
    }

    [Fact]
    public void Delta_BeatingStrongerDrivers_PaysMoreThanBeatingWeakerOnes()
    {
        // The same driver, the same win, against two very different fields.
        int[] strongField = [2000, 4000, 4000, 4000, 4000];
        int[] weakField = [2000, 1000, 1000, 1000, 1000];

        var againstStrong = IRatingCalculator.Delta(strongField, index: 0, finishPosition: 1);
        var againstWeak = IRatingCalculator.Delta(weakField, index: 0, finishPosition: 1);

        Assert.True(againstStrong > againstWeak);
    }

    [Fact]
    public void Delta_LosingToWeakerDriversCostsMoreThanLosingToStrongerOnes()
    {
        int[] strongField = [2000, 4000, 4000, 4000, 4000];
        int[] weakField = [2000, 1000, 1000, 1000, 1000];

        var lastAgainstStrong = IRatingCalculator.Delta(strongField, index: 0, finishPosition: 5);
        var lastAgainstWeak = IRatingCalculator.Delta(weakField, index: 0, finishPosition: 5);

        Assert.True(lastAgainstWeak < lastAgainstStrong);
    }

    [Fact]
    public void Delta_AFavouriteWinning_GainsLittle()
    {
        // 4000 among 1000s is expected to win, so winning barely pays.
        int[] field = [4000, 1000, 1000, 1000, 1000, 1000];

        Assert.InRange(IRatingCalculator.Delta(field, index: 0, finishPosition: 1), 0, 25);
    }

    [Fact]
    public void Delta_AnUnderdogWinning_GainsALot()
    {
        int[] field = [1000, 4000, 4000, 4000, 4000, 4000];

        Assert.True(IRatingCalculator.Delta(field, index: 0, finishPosition: 1) > 130);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void Delta_FieldTooSmall_IsZero(int count)
    {
        Assert.Equal(0, IRatingCalculator.Delta(Uniform(count, 2000), index: 0, finishPosition: 1));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(5, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 6)]
    public void Delta_OutOfRangeInputs_AreZeroRatherThanThrowing(int index, int position)
    {
        Assert.Equal(0, IRatingCalculator.Delta(Uniform(5, 2000), index, position));
    }

    [Fact]
    public void ExpectedBeaten_InAnEvenField_IsHalfTheOpponents()
    {
        Assert.Equal(4.5, IRatingCalculator.ExpectedBeaten(Uniform(10, 2500), index: 0), 6);
    }

    [Fact]
    public void ExpectedBeaten_SummedOverTheField_IsEveryPairingOnce()
    {
        int[] field = [1200, 1800, 2400, 3000, 4500, 900];

        var total = 0.0;
        for (var i = 0; i < field.Length; i++)
        {
            total += IRatingCalculator.ExpectedBeaten(field, i);
        }

        // n(n-1)/2 pairings, each contributing exactly 1.0 across both drivers.
        Assert.Equal(field.Length * (field.Length - 1) / 2.0, total, 6);
    }

    [Fact]
    public void ExpectedBeaten_RisesWithRating()
    {
        int[] field = [1000, 2000, 3000, 4000];

        var expected = Enumerable.Range(0, field.Length)
            .Select(i => IRatingCalculator.ExpectedBeaten(field, i))
            .ToList();

        Assert.Equal(expected.OrderBy(e => e), expected);
    }

    [Fact]
    public void Delta_ZeroRating_IsTreatedAsTheFloorRatherThanThrowing()
    {
        // Rookies with no rating yet shouldn't blow up the model.
        var delta = IRatingCalculator.Delta([0, 2000, 2000], index: 0, finishPosition: 1);

        Assert.True(delta > 0);
    }
}
