namespace IRacingOverlay.Core.Rating;

/// <summary>
/// The driver's rolling corners-per-incident average, carried across sessions
/// and persisted with the rest of the settings.
///
/// This exists because iRacing does not expose it. Safety Rating moves on one
/// documented rule - a session whose CPI beats your running average raises SR,
/// one below it lowers SR - but the running average itself is server-side, as
/// is the exact window it covers ("a limited history of corners", per iRacing,
/// with no published length). So the app keeps its own, from sessions it has
/// actually watched.
///
/// That makes this a <b>baseline of our own measurement</b>, not a mirror of
/// iRacing's number, and the UI is careful to say only what it can support: a
/// direction, never a predicted SR value. See <see cref="SafetyTracker"/>.
///
/// The window is a corner budget rather than a session count, matching how
/// iRacing describes its own: a 90-minute enduro should weigh more than a
/// six-lap sprint, because it is more evidence about how you drive.
/// </summary>
/// <param name="Corners">Corners in the window. Decays as newer sessions land.</param>
/// <param name="IncidentPoints">Incident points over those same corners.</param>
public sealed record CpiHistory(double Corners = 0, double IncidentPoints = 0)
{
    /// <summary>
    /// How many corners of history the baseline covers. Older evidence is scaled
    /// out proportionally once the budget is full, which approximates the
    /// recent-weighted average iRacing describes without pretending to match it.
    /// </summary>
    public const double WindowCorners = 2000;

    /// <summary>
    /// Corners needed before the baseline is worth comparing against. Below this
    /// a single early incident dominates the average, and the chip would report
    /// a confident direction from almost no evidence.
    /// </summary>
    public const double MinimumCorners = 300;

    public static readonly CpiHistory Empty = new();

    /// <summary>
    /// The baseline to compare a session against, or null when there isn't
    /// enough history to have an opinion.
    ///
    /// Also null after a spotless window (no incidents at all): the true average
    /// is then unbounded, and every session would score "worse" against it,
    /// which is exactly backwards. Showing nothing is the honest answer.
    /// </summary>
    public double? AverageCpi =>
        Corners >= MinimumCorners && IncidentPoints > 0 ? Corners / IncidentPoints : null;

    /// <summary>
    /// Folds a finished session into the window, ageing out whatever no longer
    /// fits. A session longer than the whole window is truncated to it, with its
    /// incidents scaled to the surviving portion so its CPI is preserved.
    /// </summary>
    public CpiHistory Add(double corners, double incidentPoints)
    {
        if (corners <= 0)
        {
            return this;
        }

        var admitted = Math.Min(corners, WindowCorners);
        var admittedIncidents = incidentPoints * (admitted / corners);

        // What's left of the budget once this session is in; the old history is
        // scaled down to fit it rather than dropped session by session.
        var room = WindowCorners - admitted;
        var retained = Corners > 0 ? Math.Min(1.0, room / Corners) : 0;

        return new CpiHistory(
            (Corners * retained) + admitted,
            (IncidentPoints * retained) + admittedIncidents);
    }
}
