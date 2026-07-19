using System.Collections.ObjectModel;
using System.Globalization;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Standings;
using IRacingOverlay.Core.Telemetry;
using GridLength = System.Windows.GridLength;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the full, class-grouped standings table. The rows are a single
/// flat collection (headers interleaved with car rows) so the list can be
/// updated in place every frame - the collection only changes length when the
/// field size does, which keeps ordering swaps flicker-free.
/// </summary>
public sealed class StandingsViewModel : OverlayViewModelBase
{
    private int _maxPerClass = new WidgetTuning().StandingsMaxPerClass;
    private bool _showManufacturerBadges = new OverlaySettings().ShowManufacturerBadges;

    private SessionMetadata? _metadata;
    private string _sessionTypeText = "SESSION";
    private string _sessionRemainingText = string.Empty;
    private bool _hasSessionRemaining;
    private string _carCountText = string.Empty;
    private string _lapCounterText = string.Empty;

    public StandingsViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    public ObservableCollection<StandingsRowViewModel> Items { get; } = [];

    /// <summary>Projected iRating change; hides itself outside a race.</summary>
    public IRatingChipViewModel IRating { get; } = new();

    /// <summary>The session label ("RACE") - the quiet half of the strip.</summary>
    public string SessionTypeText
    {
        get => _sessionTypeText;
        private set => SetProperty(ref _sessionTypeText, value);
    }

    /// <summary>Time or laps remaining - the strip's headline figure, typeset a
    /// step larger than everything around it.</summary>
    public string SessionRemainingText
    {
        get => _sessionRemainingText;
        private set => SetProperty(ref _sessionRemainingText, value);
    }

    /// <summary>False for an unlimited session with no lap count, so the strip
    /// shows the label alone rather than a stray separator.</summary>
    public bool HasSessionRemaining
    {
        get => _hasSessionRemaining;
        private set => SetProperty(ref _hasSessionRemaining, value);
    }

    public string CarCountText
    {
        get => _carCountText;
        private set => SetProperty(ref _carCountText, value);
    }

    public string LapCounterText
    {
        get => _lapCounterText;
        private set => SetProperty(ref _lapCounterText, value);
    }

    public override void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    /// <summary>Width of the badge column in the caption band, kept in step with
    /// the rows' own <see cref="StandingsRowViewModel.ManufacturerColumnWidth"/>.</summary>
    public GridLength ManufacturerColumnWidth
        => _showManufacturerBadges ? new GridLength(30) : new GridLength(0);

    /// <summary>Whether the "CAR" caption above the badge column is shown.</summary>
    public bool ShowManufacturerBadges => _showManufacturerBadges;

    public override void ApplySettings(OverlaySettings settings)
    {
        _maxPerClass = settings.Tuning.StandingsMaxPerClass;
        _showManufacturerBadges = settings.ShowManufacturerBadges;
        OnPropertyChanged(nameof(ManufacturerColumnWidth));
        OnPropertyChanged(nameof(ShowManufacturerBadges));

        // The rows themselves pick the change up on the next telemetry frame
        // (~66ms) - there's no cached snapshot to re-render here, and a stale
        // frame replayed through the standings would fight the in-place slot
        // updates.
    }

    public override void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var groups = StandingsCalculator.Compute(snapshot, _metadata, _maxPerClass);

        UpdateHeader(snapshot);

        // Flatten groups into interleaved header + row items.
        var plan = new List<(bool IsHeader, StandingsClassGroup Group, StandingsRow? Row)>();
        foreach (var group in groups)
        {
            plan.Add((true, group, null));
            foreach (var row in group.Rows)
            {
                plan.Add((false, group, row));
            }
        }

        // Only touch the collection itself when the line count changes (a car
        // joined/left); otherwise update the existing slots in place.
        if (Items.Count != plan.Count)
        {
            Items.Clear();
            foreach (var _ in plan)
            {
                Items.Add(new StandingsRowViewModel());
            }
        }

        var carRowIndex = 0;
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan[i].IsHeader)
            {
                Items[i].ShowHeader(plan[i].Group);
            }
            else
            {
                Items[i].ShowRow(plan[i].Row!, isAltRow: carRowIndex % 2 == 1, _showManufacturerBadges);
                carRowIndex++;
            }
        }
    }

    private void UpdateHeader(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);

        var header = SessionFormat.Header(
            sessionType, snapshot.SessionTimeRemainSeconds, snapshot.SessionLapsRemain);
        SessionTypeText = header.TypeText;
        SessionRemainingText = header.RemainingText;
        HasSessionRemaining = header.RemainingText.Length > 0;

        LapCounterText = SessionFormat.LapCounter(snapshot.Lap, _metadata?.LapsForSession(snapshot.SessionNum));

        IRating.Update(snapshot, _metadata);

        var carCount = 0;
        foreach (var car in snapshot.Cars)
        {
            if (car.Surface == CarTrackSurface.NotInWorld)
            {
                continue;
            }

            if (_metadata is not null && !_metadata.DriversByCarIdx.ContainsKey(car.CarIdx))
            {
                continue;
            }

            carCount++;
        }

        CarCountText = carCount == 1
            ? "1 CAR"
            : carCount.ToString(CultureInfo.InvariantCulture) + " CARS";
    }
}
