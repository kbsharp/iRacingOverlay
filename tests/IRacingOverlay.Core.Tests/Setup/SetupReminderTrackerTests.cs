using IRacingOverlay.Core.Setup;

namespace IRacingOverlay.Core.Tests.Setup;

public class SetupReminderTrackerTests
{
    [Fact]
    public void Update_PracticeSession_NeverFlagsOrFlashes()
    {
        var tracker = new SetupReminderTracker();

        var state = tracker.Update(0, "Practice", "practice_setup.sto", isModified: false, sessionTimeSeconds: 0);

        Assert.False(state.IsRaceOrQualify);
        Assert.False(state.ShouldFlash);
    }

    [Fact]
    public void Update_WarmupSession_NeverFlagsOrFlashes()
    {
        var tracker = new SetupReminderTracker();

        var state = tracker.Update(0, "Warmup", "race_setup.sto", isModified: false, sessionTimeSeconds: 0);

        Assert.False(state.IsRaceOrQualify);
        Assert.False(state.ShouldFlash);
    }

    [Theory]
    [InlineData("Race")]
    [InlineData("Heat Race")]
    [InlineData("Qualify")]
    [InlineData("Open Qualify")]
    [InlineData("Lone Qualify")]
    public void Update_RaceOrQualifyVariants_FlagsAndFlashesFromTheFirstFrame(string sessionType)
    {
        var tracker = new SetupReminderTracker();

        var state = tracker.Update(0, sessionType, "race_setup.sto", isModified: false, sessionTimeSeconds: 0);

        Assert.True(state.IsRaceOrQualify);
        Assert.True(state.ShouldFlash);
    }

    [Fact]
    public void Update_JustUnderTheFlashWindow_StillFlashes()
    {
        var tracker = new SetupReminderTracker();
        tracker.Update(0, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 0);

        var state = tracker.Update(0, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 59.9);

        Assert.True(state.ShouldFlash);
    }

    [Fact]
    public void Update_AtOrPastTheFlashWindow_StopsFlashingButStaysFlagged()
    {
        var tracker = new SetupReminderTracker();
        tracker.Update(0, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 0);

        var state = tracker.Update(0, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 60);

        Assert.True(state.IsRaceOrQualify);
        Assert.False(state.ShouldFlash);
    }

    [Fact]
    public void Update_SessionNumChanges_RestartsTheFlashWindow()
    {
        var tracker = new SetupReminderTracker();
        tracker.Update(0, "Qualify", "qualify_setup.sto", isModified: false, sessionTimeSeconds: 0);
        tracker.Update(0, "Qualify", "qualify_setup.sto", isModified: false, sessionTimeSeconds: 120); // window elapsed

        // A new session (race) starts - even though "sessionTimeSeconds" is
        // already large in absolute terms, the window should restart relative
        // to this transition.
        var state = tracker.Update(1, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 125);

        Assert.True(state.ShouldFlash);
    }

    [Fact]
    public void Update_PassesThroughSetupNameAndModifiedFlag()
    {
        var tracker = new SetupReminderTracker();

        var state = tracker.Update(0, "Race", "my_race_setup.sto", isModified: true, sessionTimeSeconds: 0);

        Assert.Equal("my_race_setup.sto", state.SetupName);
        Assert.True(state.IsModified);
    }

    [Fact]
    public void Update_FirstFrameAlreadyInRace_StillFlashesImmediately()
    {
        // Simulates launching the overlay mid-race rather than at session start.
        var tracker = new SetupReminderTracker();

        var state = tracker.Update(3, "Race", "race_setup.sto", isModified: false, sessionTimeSeconds: 812.4);

        Assert.True(state.ShouldFlash);
    }
}
