using System.Collections.ObjectModel;
using System.Globalization;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Standings;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Presents the full, class-grouped standings table. The rows are a single
/// flat collection (headers interleaved with car rows) so the list can be
/// updated in place every frame - the collection only changes length when the
/// field size does, which keeps ordering swaps flicker-free.
/// </summary>
public sealed class StandingsViewModel : OverlayViewModelBase
{
    private const int MaxPerClass = 12;

    private SessionMetadata? _metadata;
    private string _sessionText = "SESSION";
    private string _carCountText = string.Empty;

    public StandingsViewModel(string connectedLabel = "Live")
        : base(connectedLabel)
    {
    }

    public ObservableCollection<StandingsRowViewModel> Items { get; } = [];

    public string SessionText
    {
        get => _sessionText;
        private set => SetProperty(ref _sessionText, value);
    }

    public string CarCountText
    {
        get => _carCountText;
        private set => SetProperty(ref _carCountText, value);
    }

    public void ApplySessionMetadata(SessionMetadata metadata) => _metadata = metadata;

    public void ApplyTelemetry(TelemetrySnapshot snapshot)
    {
        var groups = StandingsCalculator.Compute(snapshot, _metadata, MaxPerClass);

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
                Items[i].ShowRow(plan[i].Row!, isAltRow: carRowIndex % 2 == 1);
                carRowIndex++;
            }
        }
    }

    private void UpdateHeader(TelemetrySnapshot snapshot)
    {
        var sessionType = SessionFormat.ResolveSessionType(_metadata?.SessionTypesByNum, snapshot.SessionNum);
        var timeRemaining = SessionFormat.TimeRemaining(snapshot.SessionTimeRemainSeconds);

        SessionText = timeRemaining is not null
            ? $"{sessionType} · {timeRemaining}"
            : snapshot.SessionLapsRemain > 0
                ? $"{sessionType} · {snapshot.SessionLapsRemain} LAPS"
                : sessionType;

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
