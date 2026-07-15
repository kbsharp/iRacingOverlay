using IRacingOverlay.Core.Demo;

namespace IRacingOverlay.Core.Tests.Demo;

public class RacePresetsTests
{
    [Fact]
    public void All_IsNonEmpty_AndDefaultIsTheFirst()
    {
        Assert.NotEmpty(RacePresets.All);
        Assert.Same(RacePresets.All[0], RacePresets.Default);
    }

    [Fact]
    public void Every_Preset_HasAConsistentClassAndShareShape()
    {
        foreach (var preset in RacePresets.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Name));
            Assert.NotEmpty(preset.Classes);
            Assert.Equal(preset.Classes.Count, preset.ClassShares.Count);
            Assert.InRange(preset.PlayerClassIndex, 0, preset.Classes.Count - 1);
            Assert.True(preset.ClassShares[preset.PlayerClassIndex] > 0);
        }
    }

    [Fact]
    public void Every_Preset_ProducesAWellFormedFieldAtItsDefaultSize()
    {
        foreach (var preset in RacePresets.All)
        {
            var plan = DemoFieldPlanner.PlanClassByCar(preset, preset.DefaultCarCount);

            Assert.Equal(preset.DefaultCarCount, plan.Length);
            Assert.Equal(preset.PlayerClassIndex, plan[0]);
            Assert.All(plan, classIndex => Assert.InRange(classIndex, 0, preset.Classes.Count - 1));
        }
    }

    [Fact]
    public void ImsaPreset_IsTheThreeClassMulticlassGrid()
    {
        var imsa = RacePresets.Default;

        Assert.Contains("IMSA", imsa.Name);
        Assert.True(imsa.IsMultiClass);
        Assert.Equal(["GTP", "LMP2", "GTD"], imsa.Classes.Select(c => c.ShortName));
    }
}
