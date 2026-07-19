using IRacingOverlay.Core.Formatting;

namespace IRacingOverlay.Core.Rating;

/// <summary>How much trust to put in the safety readout right now.</summary>
public enum SafetyOutlookState
{
    /// <summary>No corner count for the track, or a session that doesn't count — show nothing.</summary>
    Unavailable,

    /// <summary>On track, but not far enough in for CPI to mean anything yet.</summary>
    Pending,

    /// <summary>Enough laps are in the bank for the number to be worth reading.</summary>
    Live,
}

/// <summary>
/// The player's safety picture for the session so far: corners cleared per
/// incident point, and whether that is running above or below their own
/// baseline.
///
/// Deliberately <b>not</b> a projected Safety Rating value. iRacing has never
/// published the CPI-to-SR conversion, so a decimal SR delta would be invented.
/// What is documented — and what this reports — is the direction: beat your
/// average and SR rises, fall short and it drops.
/// </summary>
/// <param name="State">Whether there is anything worth showing.</param>
/// <param name="Corners">Corners cleared this session (track turns × laps completed).</param>
/// <param name="IncidentPoints">Incident points taken this session.</param>
/// <param name="SessionCpi">Corners per incident point this session;
/// <see cref="double.PositiveInfinity"/> while the session is still clean.</param>
/// <param name="BaselineCpi">The rolling average being compared against, or null
/// when there isn't enough history to have one.</param>
public sealed record SafetyOutlook(
    SafetyOutlookState State,
    double Corners,
    int IncidentPoints,
    double SessionCpi,
    double? BaselineCpi)
{
    public static readonly SafetyOutlook None =
        new(SafetyOutlookState.Unavailable, 0, 0, double.PositiveInfinity, null);

    /// <summary>True when there is a number on screen.</summary>
    public bool HasValue => State == SafetyOutlookState.Live;

    /// <summary>No incidents yet — the one case where iRacing guarantees SR rises.</summary>
    public bool IsClean => IncidentPoints == 0;

    /// <summary>True when a baseline exists, so the direction is worth drawing.</summary>
    public bool HasTrend => BaselineCpi is not null;

    /// <summary>
    /// Which way this session is pushing Safety Rating. A clean session always
    /// reads <see cref="RatingTrend.Up"/> — iRacing states outright that zero
    /// incidents always gains SR — and <see cref="RatingTrend.Flat"/> means "no
    /// baseline yet", which the UI renders as no arrow at all rather than as a
    /// claim that nothing is happening.
    /// </summary>
    public RatingTrend Trend
    {
        get
        {
            if (BaselineCpi is not { } baseline)
            {
                return RatingTrend.Flat;
            }

            return SessionCpi >= baseline ? RatingTrend.Up : RatingTrend.Down;
        }
    }
}
