using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Telemetry;

public class SessionFlagResolverTests
{
    [Fact]
    public void Resolve_PlainGreenRunning_ShowsNothing()
    {
        // The green bit stays set for the whole green-flag run, so on its own it
        // isn't worth the space in the session strip.
        Assert.Equal(SessionFlagState.None, SessionFlagResolver.Resolve(SessionFlags.Green));
    }

    [Fact]
    public void Resolve_GreenHeld_ShowsGreen()
    {
        Assert.Equal(
            SessionFlagState.Green,
            SessionFlagResolver.Resolve(SessionFlags.Green | SessionFlags.GreenHeld));
    }

    [Theory]
    [InlineData(SessionFlags.Yellow)]
    [InlineData(SessionFlags.YellowWaving)]
    [InlineData(SessionFlags.Caution)]
    [InlineData(SessionFlags.CautionWaving)]
    public void Resolve_AnyCautionBit_ReadsAsYellow(SessionFlags flags)
    {
        Assert.Equal(SessionFlagState.Yellow, SessionFlagResolver.Resolve(flags));
    }

    [Fact]
    public void Resolve_PersonalFlagsOutrankTrackFlags()
    {
        // Under a caution with a black flag out, the black flag is what the
        // driver has to act on.
        var flags = SessionFlags.Yellow | SessionFlags.Caution | SessionFlags.Black;
        Assert.Equal(SessionFlagState.Black, SessionFlagResolver.Resolve(flags));
    }

    [Fact]
    public void Resolve_DisqualifyOutranksEverything()
    {
        var flags = SessionFlags.Black | SessionFlags.Red | SessionFlags.Disqualify;
        Assert.Equal(SessionFlagState.Disqualified, SessionFlagResolver.Resolve(flags));
    }

    [Fact]
    public void Resolve_RedOutranksYellow()
    {
        Assert.Equal(
            SessionFlagState.Red,
            SessionFlagResolver.Resolve(SessionFlags.Yellow | SessionFlags.Red));
    }

    [Fact]
    public void Resolve_RepairIsMeatball()
    {
        Assert.Equal(SessionFlagState.Meatball, SessionFlagResolver.Resolve(SessionFlags.Repair));
    }

    [Fact]
    public void Resolve_NoFlags_ShowsNothing()
    {
        Assert.Equal(SessionFlagState.None, SessionFlagResolver.Resolve(SessionFlags.None));
    }

    [Fact]
    public void Label_NoFlag_IsEmpty()
    {
        Assert.Equal(string.Empty, SessionFlagResolver.Label(SessionFlagState.None));
    }

    [Theory]
    [InlineData(SessionFlagState.Yellow, "YELLOW")]
    [InlineData(SessionFlagState.Checkered, "FINISH")]
    [InlineData(SessionFlagState.Meatball, "REPAIR")]
    [InlineData(SessionFlagState.Disqualified, "DQ")]
    public void Label_UsesShortTrackside(SessionFlagState state, string expected)
    {
        Assert.Equal(expected, SessionFlagResolver.Label(state));
    }
}
