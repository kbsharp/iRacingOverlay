using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Telemetry;
using IRacingOverlay.Infrastructure.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Drives <see cref="IDemoControls"/> so the field size, fuel level, track
/// conditions, and player pit state can be changed live in demo mode,
/// without touching code or rebuilding.
/// </summary>
public sealed class DevControlViewModel : ObservableObject
{
    private readonly IDemoControls _controls;

    private string _carCountText;
    private string _raceTypeText;
    private string _wetnessText = SessionFormat.Wetness(TrackWetness.VeryLightlyWet);
    private string _sessionTypeText = "RACE";
    private bool _isSetupModified;
    private string _radarText = "CLEAR";
    private string _flagText = "NONE";

    public DevControlViewModel(IDemoControls controls)
    {
        _controls = controls;
        _carCountText = FormatCarCount();
        _raceTypeText = controls.CurrentRaceType;

        CycleRaceTypeCommand = new RelayCommand(() =>
        {
            // Switching race type rebuilds the field, so the car count changes too.
            RaceTypeText = _controls.CycleRaceType();
            CarCountText = FormatCarCount();
        });
        AddCarCommand = new RelayCommand(() => { if (_controls.AddCar()) CarCountText = FormatCarCount(); });
        RemoveCarCommand = new RelayCommand(() => { if (_controls.RemoveCar()) CarCountText = FormatCarCount(); });
        DrainFuelCommand = new RelayCommand(() => _controls.AdjustFuel(-5f));
        AddFuelCommand = new RelayCommand(() => _controls.AdjustFuel(5f));
        SetCriticalFuelCommand = new RelayCommand(_controls.SetFuelCritical);
        CycleWetnessCommand = new RelayCommand(() => WetnessText = SessionFormat.Wetness(_controls.CycleWetness()));
        AddIncidentCommand = new RelayCommand(_controls.AddIncident);
        CycleFlagCommand = new RelayCommand(() =>
            FlagText = _controls.CycleFlag().ToString().ToUpperInvariant());
        TogglePlayerPitCommand = new RelayCommand(_controls.TogglePlayerPit);
        CycleSessionCommand = new RelayCommand(() =>
        {
            SessionTypeText = _controls.CycleSessionType().ToUpperInvariant();
            IsSetupModified = false; // CycleSessionType resets this in the source too
        });
        ToggleSetupModifiedCommand = new RelayCommand(() =>
        {
            _controls.ToggleSetupModified();
            IsSetupModified = !IsSetupModified;
        });
        CycleRadarCommand = new RelayCommand(() =>
            RadarText = _controls.CycleCarLeftRight().ToString().ToUpperInvariant());
    }

    public ICommand CycleRaceTypeCommand { get; }

    public ICommand AddCarCommand { get; }

    public ICommand RemoveCarCommand { get; }

    public ICommand DrainFuelCommand { get; }

    public ICommand AddFuelCommand { get; }

    public ICommand SetCriticalFuelCommand { get; }

    public ICommand CycleWetnessCommand { get; }

    public ICommand AddIncidentCommand { get; }

    public ICommand TogglePlayerPitCommand { get; }

    public ICommand CycleSessionCommand { get; }

    public ICommand ToggleSetupModifiedCommand { get; }

    public ICommand CycleRadarCommand { get; }

    public ICommand CycleFlagCommand { get; }

    public string CarCountText
    {
        get => _carCountText;
        private set => SetProperty(ref _carCountText, value);
    }

    public string RaceTypeText
    {
        get => _raceTypeText;
        private set => SetProperty(ref _raceTypeText, value);
    }

    public string WetnessText
    {
        get => _wetnessText;
        private set => SetProperty(ref _wetnessText, value);
    }

    public string SessionTypeText
    {
        get => _sessionTypeText;
        private set => SetProperty(ref _sessionTypeText, value);
    }

    public bool IsSetupModified
    {
        get => _isSetupModified;
        private set => SetProperty(ref _isSetupModified, value);
    }

    public string RadarText
    {
        get => _radarText;
        private set => SetProperty(ref _radarText, value);
    }

    public string FlagText
    {
        get => _flagText;
        private set => SetProperty(ref _flagText, value);
    }

    private string FormatCarCount() => $"{_controls.CarCount} cars ({_controls.MinCarCount}-{_controls.MaxCarCount})";
}
