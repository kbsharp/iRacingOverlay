using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Strategy;

namespace IRacingOverlay.Core.Tests.Formatting;

public class TrafficFormatTests
{
    [Fact]
    public void CarLabel_IsClassAndNumber()
    {
        var threat = Threat(laps: 1.5, sector: 2);

        Assert.Equal("GTP #63", TrafficFormat.CarLabel(threat));
    }

    [Fact]
    public void Meeting_ReadsWhenThenWhere()
    {
        Assert.Equal("next lap · sector 2", TrafficFormat.Meeting(Threat(laps: 1.5, sector: 2)));
        Assert.Equal("this lap · sector 3", TrafficFormat.Meeting(Threat(laps: 0.4, sector: 3)));
        Assert.Equal("in 2 laps · sector 1", TrafficFormat.Meeting(Threat(laps: 2.6, sector: 1)));
    }

    [Fact]
    public void Meeting_DropsSectorWhenUnknown()
    {
        Assert.Equal("next lap", TrafficFormat.Meeting(Threat(laps: 1.2, sector: null)));
    }

    [Fact]
    public void NoThreat_IsAllEmpty()
    {
        Assert.Equal(string.Empty, TrafficFormat.CarLabel(TrafficThreat.None));
        Assert.Equal(string.Empty, TrafficFormat.Meeting(TrafficThreat.None));
    }

    [Fact]
    public void CarLabel_OmitsNumberWhenMissing()
    {
        var threat = new TrafficThreat("", "GTP", "e0532e", 5, 4, 1.2, 2);

        Assert.Equal("GTP", TrafficFormat.CarLabel(threat));
    }

    private static TrafficThreat Threat(double laps, int? sector) =>
        new(
            CarNumber: "63",
            ClassShortName: "GTP",
            ClassColorRaw: "e0532e",
            GapSeconds: 5,
            ClosingRateSecondsPerLap: 4,
            LapsToContact: laps,
            MeetingSector: sector);
}
