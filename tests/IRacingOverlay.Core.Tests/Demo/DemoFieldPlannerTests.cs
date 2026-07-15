using IRacingOverlay.Core.Demo;

namespace IRacingOverlay.Core.Tests.Demo;

public class DemoFieldPlannerTests
{
    private static readonly RaceClass A = new("A", "FF0000", 100.0);
    private static readonly RaceClass B = new("B", "00FF00", 105.0);
    private static readonly RaceClass C = new("C", "0000FF", 110.0);

    [Fact]
    public void ClassCounts_SingleClass_PutsEveryCarInThatClass()
    {
        var preset = new RacePreset("Solo", [A], [1], playerClassIndex: 0, defaultCarCount: 20);

        var counts = DemoFieldPlanner.ClassCounts(preset, 20);

        Assert.Equal([20], counts);
    }

    [Fact]
    public void ClassCounts_SumsToTheRequestedField()
    {
        var preset = new RacePreset("Tri", [A, B, C], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24);

        for (var carCount = 3; carCount <= 40; carCount++)
        {
            var counts = DemoFieldPlanner.ClassCounts(preset, carCount);
            Assert.Equal(carCount, counts.Sum());
        }
    }

    [Fact]
    public void ClassCounts_SplitsProportionallyToShares()
    {
        // 5:6:13 over 24 cars lands exactly on the shares.
        var preset = new RacePreset("Tri", [A, B, C], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24);

        var counts = DemoFieldPlanner.ClassCounts(preset, 24);

        Assert.Equal([5, 6, 13], counts);
    }

    [Fact]
    public void ClassCounts_GivesEveryClassAtLeastOne_WhenFieldIsBigEnough()
    {
        // A share this lopsided would floor the small classes to zero without the
        // minimum-seat guarantee.
        var preset = new RacePreset("Lopsided", [A, B, C], [1, 1, 50], playerClassIndex: 2, defaultCarCount: 30);

        var counts = DemoFieldPlanner.ClassCounts(preset, 30);

        Assert.All(counts, c => Assert.True(c >= 1));
        Assert.Equal(30, counts.Sum());
    }

    [Fact]
    public void ClassCounts_AlwaysSeatsThePlayerClass_EvenAtMinimumField()
    {
        var preset = new RacePreset("Tri", [A, B, C], [50, 50, 1], playerClassIndex: 2, defaultCarCount: 24);

        var counts = DemoFieldPlanner.ClassCounts(preset, 3);

        Assert.True(counts[2] >= 1);
        Assert.Equal(3, counts.Sum());
    }

    [Fact]
    public void PlanClassByCar_PutsThePlayerInThePlayerClass()
    {
        var preset = new RacePreset("Tri", [A, B, C], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24);

        var plan = DemoFieldPlanner.PlanClassByCar(preset, 24);

        Assert.Equal(2, plan[0]);
    }

    [Fact]
    public void PlanClassByCar_MatchesTheClassCounts()
    {
        var preset = new RacePreset("Tri", [A, B, C], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24);

        var plan = DemoFieldPlanner.PlanClassByCar(preset, 24);
        var expected = DemoFieldPlanner.ClassCounts(preset, 24);

        var actual = new int[expected.Length];
        foreach (var classIndex in plan)
        {
            actual[classIndex]++;
        }

        Assert.Equal(expected, actual);
        Assert.Equal(24, plan.Length);
    }

    [Fact]
    public void PlanClassByCar_IsDeterministic()
    {
        var preset = new RacePreset("Tri", [A, B, C], [5, 6, 13], playerClassIndex: 2, defaultCarCount: 24);

        Assert.Equal(DemoFieldPlanner.PlanClassByCar(preset, 17), DemoFieldPlanner.PlanClassByCar(preset, 17));
    }
}
