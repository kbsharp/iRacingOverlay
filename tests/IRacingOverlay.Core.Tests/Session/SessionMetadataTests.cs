using IRacingOverlay.Core.Session;

namespace IRacingOverlay.Core.Tests.Session;

public class SessionMetadataTests
{
    private static SessionMetadata Build(IReadOnlyDictionary<int, int>? lapsByNum) =>
        new(
            new Dictionary<int, RosterDriver>(),
            new Dictionary<int, string>(),
            PlayerSetupName: string.Empty,
            PlayerSetupIsModified: false,
            SessionLapsByNum: lapsByNum);

    [Fact]
    public void LapsForSession_ReturnsTheScheduledDistance()
    {
        var metadata = Build(new Dictionary<int, int> { [2] = 25 });
        Assert.Equal(25, metadata.LapsForSession(2));
    }

    [Fact]
    public void LapsForSession_TimedSession_IsNull()
    {
        // Timed sessions are simply absent from the map (the sim reports
        // "unlimited", which the parser drops).
        var metadata = Build(new Dictionary<int, int> { [2] = 25 });
        Assert.Null(metadata.LapsForSession(1));
    }

    [Fact]
    public void LapsForSession_NoLapDataAtAll_IsNull()
    {
        Assert.Null(Build(null).LapsForSession(0));
    }
}
