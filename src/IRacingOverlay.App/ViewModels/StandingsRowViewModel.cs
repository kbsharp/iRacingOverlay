using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Standings;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One line of the standings list. A line is either a class-group header or a
/// car row; the same view-model type serves both so the list can be a single
/// flat collection updated in place (no per-frame collection churn or flicker).
/// </summary>
public sealed class StandingsRowViewModel : ObservableObject
{
    private bool _isHeader;
    private bool _isRow;

    // Header
    private string _className = string.Empty;
    private string _classCountText = string.Empty;
    private Brush _headerColorBrush = Brushes.Gray;

    // Row
    private bool _isPlayer;
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
    private string _lastText = string.Empty;
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

    public string LastText
    {
        get => _lastText;
        private set => SetProperty(ref _lastText, value);
    }

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
        ClassCountText = group.Rows.Count.ToString(CultureInfo.InvariantCulture);
        HeaderColorBrush = ViewModels.ClassColorBrush.Resolve(group.ClassColorHex);
    }

    public void ShowRow(StandingsRow row)
    {
        IsHeader = false;
        IsRow = true;
        IsPlayer = row.IsPlayer;
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
        LastText = StandingsFormat.LapTime(row.LastLapSeconds);
        GapText = StandingsFormat.Gap(row.GapToClassLeaderSeconds, row.LapsDown);
    }
}
