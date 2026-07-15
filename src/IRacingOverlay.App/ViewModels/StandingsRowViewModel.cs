using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Standings;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One line of the standings list — either a class-group header or a car row.
/// One view-model type serves both so the list can be a single flat collection
/// updated in place (no per-frame collection churn or flicker).
/// </summary>
public sealed class StandingsRowViewModel : ObservableObject
{
    private bool _isHeader;
    private bool _isRow;

    // Header
    private string _className = string.Empty;
    private string _classSofText = string.Empty;
    private string _classCountText = string.Empty;
    private Brush _headerColorBrush = Brushes.Gray;

    // Row
    private bool _isPlayer;
    private bool _isAltRow;
    private bool _inPits;
    private string _positionText = string.Empty;
    private string _carNumberText = string.Empty;
    private string _name = string.Empty;
    private string _license = string.Empty;
    private LicenseTier _licenseTier;
    private string _iRatingText = string.Empty;
    private IRatingTier _iRatingTier;
    private Brush _classColorBrush = Brushes.Gray;
    private string _bestText = string.Empty;
    private bool _isSessionBest;
    private string _lastDeltaText = string.Empty;
    private bool _lastDeltaIsSlower;
    private string _intervalText = string.Empty;
    private string _gapText = string.Empty;

    public bool IsHeader
    {
        get => _isHeader;
        private set => SetProperty(ref _isHeader, value);
    }

    public bool IsRow
    {
        get => _isRow;
        private set => SetProperty(ref _isRow, value);
    }

    public string ClassName
    {
        get => _className;
        private set => SetProperty(ref _className, value);
    }

    public string ClassSofText
    {
        get => _classSofText;
        private set => SetProperty(ref _classSofText, value);
    }

    public string ClassCountText
    {
        get => _classCountText;
        private set => SetProperty(ref _classCountText, value);
    }

    public Brush HeaderColorBrush
    {
        get => _headerColorBrush;
        private set => SetProperty(ref _headerColorBrush, value);
    }

    public bool IsPlayer
    {
        get => _isPlayer;
        private set => SetProperty(ref _isPlayer, value);
    }

    /// <summary>Every other car row gets a faint background stripe.</summary>
    public bool IsAltRow
    {
        get => _isAltRow;
        private set => SetProperty(ref _isAltRow, value);
    }

    public bool InPits
    {
        get => _inPits;
        private set => SetProperty(ref _inPits, value);
    }

    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    public string CarNumberText
    {
        get => _carNumberText;
        private set => SetProperty(ref _carNumberText, value);
    }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string License
    {
        get => _license;
        private set => SetProperty(ref _license, value);
    }

    public LicenseTier LicenseTier
    {
        get => _licenseTier;
        private set => SetProperty(ref _licenseTier, value);
    }

    public string IRatingText
    {
        get => _iRatingText;
        private set => SetProperty(ref _iRatingText, value);
    }

    public IRatingTier IRatingTier
    {
        get => _iRatingTier;
        private set => SetProperty(ref _iRatingTier, value);
    }

    public Brush ClassColorBrush
    {
        get => _classColorBrush;
        private set => SetProperty(ref _classColorBrush, value);
    }

    public string BestText
    {
        get => _bestText;
        private set => SetProperty(ref _bestText, value);
    }

    /// <summary>True when this car holds the fastest lap of the session (rendered purple).</summary>
    public bool IsSessionBest
    {
        get => _isSessionBest;
        private set => SetProperty(ref _isSessionBest, value);
    }

    /// <summary>Last lap versus this car's own best, e.g. "+0.4" / "-0.2".</summary>
    public string LastDeltaText
    {
        get => _lastDeltaText;
        private set => SetProperty(ref _lastDeltaText, value);
    }

    public bool LastDeltaIsSlower
    {
        get => _lastDeltaIsSlower;
        private set => SetProperty(ref _lastDeltaIsSlower, value);
    }

    /// <summary>Interval to the car directly ahead in class.</summary>
    public string IntervalText
    {
        get => _intervalText;
        private set => SetProperty(ref _intervalText, value);
    }

    /// <summary>Gap to the class leader.</summary>
    public string GapText
    {
        get => _gapText;
        private set => SetProperty(ref _gapText, value);
    }

    public void ShowHeader(StandingsClassGroup group)
    {
        IsHeader = true;
        IsRow = false;
        ClassName = group.ClassShortName.Length > 0 ? group.ClassShortName : "FIELD";
        ClassSofText = group.StrengthOfField > 0
            ? "SoF " + group.StrengthOfField.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        ClassCountText = group.Rows.Count.ToString(CultureInfo.InvariantCulture);
        HeaderColorBrush = ViewModels.ClassColorBrush.Resolve(group.ClassColorHex);
    }

    public void ShowRow(StandingsRow row, bool isAltRow)
    {
        IsHeader = false;
        IsRow = true;
        IsPlayer = row.IsPlayer;
        IsAltRow = isAltRow;
        InPits = row.InPits;
        PositionText = row.ClassPosition > 0
            ? row.ClassPosition.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        CarNumberText = row.CarNumber.Length > 0 ? "#" + row.CarNumber : string.Empty;
        Name = row.DisplayName;
        License = row.License;
        LicenseTier = row.LicenseTier;
        IRatingText = row.IRating > 0 ? SessionFormat.IRating(row.IRating) : string.Empty;
        IRatingTier = row.IRatingTier;
        ClassColorBrush = ViewModels.ClassColorBrush.Resolve(row.ClassColorHex);
        BestText = StandingsFormat.LapTime(row.BestLapSeconds);
        IsSessionBest = row.IsSessionBestLap;
        LastDeltaText = row.LastDeltaSeconds is { } d ? SessionFormat.Delta(d) : TelemetryFormat.Placeholder;
        LastDeltaIsSlower = row.LastDeltaSeconds is > 0;
        IntervalText = StandingsFormat.Gap(row.IntervalSeconds, row.IntervalLapsDown);
        GapText = StandingsFormat.Gap(row.GapToClassLeaderSeconds, row.GapLapsDown);
    }
}
