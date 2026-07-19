using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.Core.Tests.Formatting;

public class SessionFormatTests
{
    [Fact]
    public void Header_TimedSession_PutsTheClockInTheFigure()
    {
        var header = SessionFormat.Header("RACE", 204, 0);

        Assert.Equal("RACE", header.TypeText);
        Assert.Equal("3:24", header.RemainingText);
    }

    [Fact]
    public void Header_LapLimitedSession_FallsBackToLaps()
    {
        // Unlimited time, but a real lap count: the laps are the headline.
        var header = SessionFormat.Header("RACE", 604000, 12);

        Assert.Equal("RACE", header.TypeText);
        Assert.Equal("12 LAPS", header.RemainingText);
    }

    [Fact]
    public void Header_TimePreferredOverLaps()
    {
        var header = SessionFormat.Header("RACE", 204, 12);

        Assert.Equal("3:24", header.RemainingText);
    }

    [Fact]
    public void Header_UnlimitedWithNoLaps_HasNoFigure()
    {
        // The strip shows the label alone rather than a stray separator.
        var header = SessionFormat.Header("PRACTICE", 604000, 0);

        Assert.Equal("PRACTICE", header.TypeText);
        Assert.Equal(string.Empty, header.RemainingText);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(59, "0:59")]
    [InlineData(1185, "19:45")]
    [InlineData(3723, "1:02:03")]
    public void TimeRemaining_FormatsMinutesAndHours(double seconds, string expected)
    {
        Assert.Equal(expected, SessionFormat.TimeRemaining(seconds));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(604800)] // iRacing's "unlimited" sentinel
    public void TimeRemaining_UnknownOrUnlimited_ReturnsNull(double seconds)
    {
        Assert.Null(SessionFormat.TimeRemaining(seconds));
    }

    [Theory]
    [InlineData(850, "850")]
    [InlineData(1049, "1.0k")]
    [InlineData(3449, "3.4k")]
    [InlineData(11205, "11.2k")]
    public void IRating_FormatsInThousands(int irating, string expected)
    {
        Assert.Equal(expected, SessionFormat.IRating(irating));
    }

    [Theory]
    [InlineData(1.23, "+1.2")]
    [InlineData(-0.81, "-0.8")]
    [InlineData(0, "+0.0")]
    public void Delta_FormatsWithExplicitSign(double seconds, string expected)
    {
        Assert.Equal(expected, SessionFormat.Delta(seconds));
    }

    [Theory]
    [InlineData(TrackWetness.Dry, "DRY")]
    [InlineData(TrackWetness.Unknown, "DRY")]
    [InlineData(TrackWetness.MostlyDry, "DRYING")]
    [InlineData(TrackWetness.LightlyWet, "DAMP")]
    [InlineData(TrackWetness.ModeratelyWet, "WET")]
    [InlineData(TrackWetness.ExtremelyWet, "V.WET")]
    public void Wetness_MapsToShortLabels(TrackWetness wetness, string expected)
    {
        Assert.Equal(expected, SessionFormat.Wetness(wetness));
    }

    [Fact]
    public void Temperature_RoundsToWholeDegrees()
    {
        Assert.Equal("41°", SessionFormat.Temperature(40.6f));
    }

    [Theory]
    [InlineData("17", 17)]
    [InlineData("  25 ", 25)]
    public void ParseLimit_ReadsNumericLimits(string raw, int expected)
    {
        Assert.Equal(expected, SessionFormat.ParseLimit(raw));
    }

    [Theory]
    [InlineData("unlimited")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    public void ParseLimit_UnlimitedOrJunk_ReturnsNull(string? raw)
    {
        Assert.Null(SessionFormat.ParseLimit(raw));
    }

    [Fact]
    public void Incidents_WithLimit_ShowsBothSides()
    {
        Assert.Equal("4x/17x", SessionFormat.Incidents(4, 17));
    }

    [Fact]
    public void Incidents_Unlimited_ShowsCountOnly()
    {
        Assert.Equal("4x", SessionFormat.Incidents(4, null));
    }

    [Theory]
    [InlineData(0, 17, IncidentSeverity.Ok)]
    [InlineData(11, 17, IncidentSeverity.Ok)]     // 0.65
    [InlineData(12, 17, IncidentSeverity.Warning)] // 0.71
    [InlineData(16, 17, IncidentSeverity.Critical)] // 0.94
    [InlineData(17, 17, IncidentSeverity.Critical)]
    public void IncidentLevel_WarnsBeforeTheLimit(int count, int limit, IncidentSeverity expected)
    {
        Assert.Equal(expected, SessionFormat.IncidentLevel(count, limit));
    }

    [Fact]
    public void IncidentLevel_Unlimited_IsAlwaysOk()
    {
        Assert.Equal(IncidentSeverity.Ok, SessionFormat.IncidentLevel(300, null));
    }

    [Fact]
    public void LapCounter_LapLimited_ShowsCurrentAndTotal()
    {
        Assert.Equal("L12/25", SessionFormat.LapCounter(12, 25));
    }

    [Fact]
    public void LapCounter_Timed_ShowsCurrentOnly()
    {
        Assert.Equal("L12", SessionFormat.LapCounter(12, null));
    }

    [Fact]
    public void LapCounter_PastTheTotal_ClampsToTheRaceDistance()
    {
        // The sim keeps counting on the cool-down lap; "L26/25" reads as a bug.
        Assert.Equal("L25/25", SessionFormat.LapCounter(26, 25));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LapCounter_BeforeTheFirstLap_IsEmpty(int lap)
    {
        Assert.Equal(string.Empty, SessionFormat.LapCounter(lap, 25));
    }

    // ShortType exists to keep the session strip inside its width budget - the
    // long names are what pushed the flag chip into the telemetry group.
    [Theory]
    [InlineData("Open Qualify", "QUALIFY")]
    [InlineData("Lone Qualify", "QUALIFY")]
    [InlineData("Qualify", "QUALIFY")]
    [InlineData("Practice", "PRACTICE")]
    [InlineData("Race", "RACE")]
    [InlineData("Heat Race", "RACE")]
    [InlineData("Warmup", "WARMUP")]
    [InlineData("Offline Testing", "TESTING")]
    public void ShortType_ReducesTheSessionToOneWord(string sessionType, string expected)
    {
        Assert.Equal(expected, SessionFormat.ShortType(sessionType));
    }

    [Fact]
    public void ShortType_UnknownSession_PassesThroughUppercasedRatherThanBlank()
    {
        // A name we don't recognise is still worth showing - dropping it would
        // leave the strip with no session label at all.
        Assert.Equal("FEATURE EVENT", SessionFormat.ShortType("Feature Event"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ShortType_NoSession_IsEmpty(string? sessionType)
    {
        Assert.Equal(string.Empty, SessionFormat.ShortType(sessionType!));
    }

    [Fact]
    public void Header_ShortensTheSessionLabel()
    {
        var header = SessionFormat.Header("Open Qualify", 183, 0);

        Assert.Equal("QUALIFY", header.TypeText);
        Assert.Equal("3:03", header.RemainingText);
    }
}
