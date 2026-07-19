using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;
using IRacingOverlay.Core.Settings;
using GridLength = System.Windows.GridLength;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One fixed display slot of the relative widget. Slots are updated in
/// place every frame rather than recreated, to keep the UI allocation-free
/// and the layout stable.
/// </summary>
public sealed class RelativeRowViewModel : ObservableObject
{
    /// <summary>Width the trend column takes when it's switched on.</summary>
    private const double TrendColumnPixels = 52;

    private static readonly Brush FallbackClassBrush = Brushes.Gray;

    private bool _isVisible;
    private bool _isPlayer;
    private bool _isAltRow;
    private bool _inPits;
    private string _positionText = string.Empty;
    private string _carNumberText = string.Empty;
    private string _name = string.Empty;
    private string _license = string.Empty;
    private LicenseTier _licenseTier;
    private string _iRatingText = string.Empty;
    private string _classShortName = string.Empty;
    private Brush _classColorBrush = FallbackClassBrush;
    private string _deltaText = string.Empty;
    private LapDifference _lapDifference;
    private string _trendArrow = string.Empty;
    private string _trendRateText = string.Empty;
    private string _trendLapsText = string.Empty;
    private PaceTrendTone _trendTone;
    private bool _showPaceTrend = new OverlaySettings().ShowPaceTrend;

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public bool IsPlayer
    {
        get => _isPlayer;
        private set => SetProperty(ref _isPlayer, value);
    }

    /// <summary>Fixed by the slot's position (set once), so the zebra striping
    /// stays stable as rows update in place.</summary>
    public bool IsAltRow
    {
        get => _isAltRow;
        set => SetProperty(ref _isAltRow, value);
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

    public string ClassShortName
    {
        get => _classShortName;
        private set => SetProperty(ref _classShortName, value);
    }

    /// <summary>The car's class colour as reported by the sim, or a neutral grey
    /// fallback for single-class sessions and malformed/missing data.</summary>
    public Brush ClassColorBrush
    {
        get => _classColorBrush;
        private set => SetProperty(ref _classColorBrush, value);
    }

    public string DeltaText
    {
        get => _deltaText;
        private set => SetProperty(ref _deltaText, value);
    }

    public LapDifference LapDifference
    {
        get => _lapDifference;
        private set => SetProperty(ref _lapDifference, value);
    }

    /// <summary>Which way the gap is moving: "▼" shrinking, "▲" growing.</summary>
    public string TrendArrow
    {
        get => _trendArrow;
        private set => SetProperty(ref _trendArrow, value);
    }

    /// <summary>How fast, in seconds per lap; <see cref="TrendArrow"/> carries the sign.</summary>
    public string TrendRateText
    {
        get => _trendRateText;
        private set => SetProperty(ref _trendRateText, value);
    }

    /// <summary>Laps until the battle arrives, shown only when it arrives before the flag.</summary>
    public string TrendLapsText
    {
        get => _trendLapsText;
        private set => SetProperty(ref _trendLapsText, value);
    }

    /// <summary>Drives the trend's colour: a catch you make, a catch made on you, or neither.</summary>
    public PaceTrendTone TrendTone
    {
        get => _trendTone;
        private set => SetProperty(ref _trendTone, value);
    }

    /// <summary>Whether this row carries the catch/defend column at all. Off by
    /// default - see <see cref="OverlaySettings.ShowPaceTrend"/>.</summary>
    public bool ShowPaceTrend
    {
        get => _showPaceTrend;
        set
        {
            if (SetProperty(ref _showPaceTrend, value))
            {
                OnPropertyChanged(nameof(TrendColumnWidth));
            }
        }
    }

    /// <summary>The trend column's width, so switching it off reclaims the space
    /// for the name rather than leaving a gap. Bound by the row grid directly:
    /// a collapsed child still holds its column open otherwise.</summary>
    public GridLength TrendColumnWidth
        => ShowPaceTrend ? new GridLength(TrendColumnPixels) : new GridLength(0);

    public void Show(RelativeRow row, PaceTrend trend)
    {
        IsVisible = true;
        IsPlayer = row.IsPlayer;
        InPits = row.InPits;
        PositionText = row.Position > 0
            ? row.Position.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        CarNumberText = row.CarNumber.Length > 0 ? "#" + row.CarNumber : string.Empty;
        Name = row.DisplayName;
        License = row.License;
        LicenseTier = row.LicenseTier;
        IRatingText = row.IRating > 0 ? SessionFormat.IRating(row.IRating) : string.Empty;
        ClassShortName = row.ClassShortName;
        ClassColorBrush = ParseClassColor(row.ClassColorHex);
        DeltaText = row.IsPlayer ? string.Empty : SessionFormat.Delta(row.DeltaSeconds);
        LapDifference = row.LapDifference;
        ShowTrend(row.IsPlayer ? PaceTrend.None : trend, isAhead: row.DeltaSeconds > 0);
    }

    public void Hide()
    {
        IsVisible = false;
        IsPlayer = false;
        InPits = false;
        PositionText = string.Empty;
        CarNumberText = string.Empty;
        Name = string.Empty;
        License = string.Empty;
        LicenseTier = LicenseTier.Unknown;
        IRatingText = string.Empty;
        ClassShortName = string.Empty;
        ClassColorBrush = FallbackClassBrush;
        DeltaText = string.Empty;
        LapDifference = LapDifference.SameLap;
        ShowTrend(PaceTrend.None, isAhead: false);
    }

    private void ShowTrend(PaceTrend trend, bool isAhead)
    {
        TrendArrow = PaceTrendFormat.Arrow(trend);
        TrendRateText = PaceTrendFormat.Rate(trend);
        TrendLapsText = PaceTrendFormat.LapsToContact(trend);
        TrendTone = PaceTrendFormat.Tone(trend, isAhead);
    }

    private static Brush ParseClassColor(string? hex)
    {
        if (hex is null)
        {
            return FallbackClassBrush;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return FallbackClassBrush;
        }
    }
}
