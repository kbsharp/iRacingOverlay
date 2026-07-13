using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Relative;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One fixed display slot of the relative widget. Slots are updated in
/// place every frame rather than recreated, to keep the UI allocation-free
/// and the layout stable.
/// </summary>
public sealed class RelativeRowViewModel : ObservableObject
{
    private bool _isVisible;
    private bool _isPlayer;
    private bool _inPits;
    private string _positionText = string.Empty;
    private string _carNumberText = string.Empty;
    private string _name = string.Empty;
    private string _license = string.Empty;
    private string _iRatingText = string.Empty;
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

    public string IRatingText
    {
        get => _iRatingText;
        private set => SetProperty(ref _iRatingText, value);
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
        IRatingText = row.IRating > 0 ? SessionFormat.IRating(row.IRating) : string.Empty;
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
        IRatingText = string.Empty;
        DeltaText = string.Empty;
        LapDifference = LapDifference.SameLap;
    }
}
