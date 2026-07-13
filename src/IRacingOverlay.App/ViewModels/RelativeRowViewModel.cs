using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;
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
    private static readonly Brush FallbackClassBrush = Brushes.Gray;

    private bool _isVisible;
    private bool _isPlayer;
    private bool _inPits;
    private string _positionText = string.Empty;
    private string _carNumberText = string.Empty;
    private string _name = string.Empty;
    private string _license = string.Empty;
    private LicenseTier _licenseTier;
    private string _iRatingText = string.Empty;
    private IRatingTier _iRatingTier;
    private string _classShortName = string.Empty;
    private Brush _classColorBrush = FallbackClassBrush;
    private string _deltaText = string.Empty;
    private LapDifference _lapDifference;

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

    public void Show(RelativeRow row)
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
        IRatingTier = row.IRatingTier;
        ClassShortName = row.ClassShortName;
        ClassColorBrush = ParseClassColor(row.ClassColorHex);
        DeltaText = row.IsPlayer ? string.Empty : SessionFormat.Delta(row.DeltaSeconds);
        LapDifference = row.LapDifference;
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
        IRatingTier = IRatingTier.Low;
        ClassShortName = string.Empty;
        ClassColorBrush = FallbackClassBrush;
        DeltaText = string.Empty;
        LapDifference = LapDifference.SameLap;
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
