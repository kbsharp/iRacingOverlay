using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IRacingOverlay.App.Services;
using IRacingOverlay.Core.Settings;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// One widget's row in the settings window: its toggles and its scale override.
/// Setting any property writes straight through to the settings service, which
/// raises Changed and makes the app re-apply - there is no OK/Apply button,
/// because the whole point is seeing the overlay react while you adjust it.
/// </summary>
public sealed class WidgetSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly string _id;
    private bool _isEnabled;
    private bool _isClickThrough;
    private double _scale;

    public WidgetSettingsViewModel(SettingsService settings, string id, string displayName)
    {
        _settings = settings;
        _id = id;
        DisplayName = displayName;

        _isEnabled = settings.Current.IsWidgetEnabled(id);
        _isClickThrough = settings.Current.IsClickThrough(id);
        _scale = settings.Current.ScaleFor(id);
    }

    public string DisplayName { get; }

    /// <summary>The scales offered per widget - the tray's four, so the two
    /// surfaces can't disagree about what's available.</summary>
    public IReadOnlyList<double> ScaleOptions { get; } = [1.0, 1.25, 1.5, 1.75];

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                _settings.SetWidgetEnabled(_id, value);
            }
        }
    }

    public bool IsClickThrough
    {
        get => _isClickThrough;
        set
        {
            if (SetProperty(ref _isClickThrough, value))
            {
                _settings.SetClickThrough(_id, value);
            }
        }
    }

    public double Scale
    {
        get => _scale;
        set
        {
            if (SetProperty(ref _scale, value))
            {
                _settings.SetWidgetScale(_id, value);
            }
        }
    }

    /// <summary>Re-reads from settings after an external change (the tray toggling
    /// the same widget, or Reset). Assigns the backing fields directly so it
    /// doesn't write back and loop.</summary>
    public void Refresh(OverlaySettings settings)
    {
        SetProperty(ref _isEnabled, settings.IsWidgetEnabled(_id), nameof(IsEnabled));
        SetProperty(ref _isClickThrough, settings.IsClickThrough(_id), nameof(IsClickThrough));
        SetProperty(ref _scale, settings.ScaleFor(_id), nameof(Scale));
    }
}

/// <summary>
/// Backs the settings window. Like the widget rows above, every setter writes
/// through immediately - the overlay updates live, so the window is a control
/// panel rather than a form to fill in and submit.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private bool _isRefreshing;
    private bool _runAtStartup;

    public SettingsViewModel(
        SettingsService settings,
        IReadOnlyList<(string Id, string DisplayName)> widgets)
    {
        _settings = settings;

        foreach (var (id, displayName) in widgets)
        {
            Widgets.Add(new WidgetSettingsViewModel(settings, id, displayName));
        }

        _runAtStartup = settings.Current.RunAtStartup;
        ResetLayoutCommand = new RelayCommand(settings.ResetLayout);

        settings.Changed += OnSettingsChanged;
    }

    public ObservableCollection<WidgetSettingsViewModel> Widgets { get; } = [];

    public IRelayCommand ResetLayoutCommand { get; }

    // ---- Units -------------------------------------------------------------
    // Exposed as bools rather than the enums so each pair can be a plain radio
    // button in XAML without needing an enum-to-bool converter.

    public bool FuelInLiters
    {
        get => _settings.Current.Units.Fuel == FuelUnit.Liters;
        set => SetUnits(_settings.Current.Units with { Fuel = value ? FuelUnit.Liters : FuelUnit.Gallons });
    }

    public bool FuelInGallons
    {
        get => !FuelInLiters;
        set => FuelInLiters = !value;
    }

    public bool TemperatureInCelsius
    {
        get => _settings.Current.Units.Temperature == TemperatureUnit.Celsius;
        set => SetUnits(_settings.Current.Units with
        {
            Temperature = value ? TemperatureUnit.Celsius : TemperatureUnit.Fahrenheit,
        });
    }

    public bool TemperatureInFahrenheit
    {
        get => !TemperatureInCelsius;
        set => TemperatureInCelsius = !value;
    }

    public bool SpeedInKph
    {
        get => _settings.Current.Units.Speed == SpeedUnit.Kph;
        set => SetUnits(_settings.Current.Units with { Speed = value ? SpeedUnit.Kph : SpeedUnit.Mph });
    }

    public bool SpeedInMph
    {
        get => !SpeedInKph;
        set => SpeedInKph = !value;
    }

    // ---- Tuning ------------------------------------------------------------

    public double FuelSafetyMarginLaps
    {
        get => _settings.Current.Tuning.FuelSafetyMarginLaps;
        set => SetTuning(_settings.Current.Tuning with { FuelSafetyMarginLaps = value });
    }

    public double SetupFlashSeconds
    {
        get => _settings.Current.Tuning.SetupFlashSeconds;
        set => SetTuning(_settings.Current.Tuning with { SetupFlashSeconds = value });
    }

    public double RadarRangeMeters
    {
        get => _settings.Current.Tuning.RadarRangeMeters;
        set => SetTuning(_settings.Current.Tuning with { RadarRangeMeters = value });
    }

    public int RelativeSlotsPerSide
    {
        get => _settings.Current.Tuning.RelativeSlotsPerSide;
        set => SetTuning(_settings.Current.Tuning with { RelativeSlotsPerSide = value });
    }

    public int StandingsMaxPerClass
    {
        get => _settings.Current.Tuning.StandingsMaxPerClass;
        set => SetTuning(_settings.Current.Tuning with { StandingsMaxPerClass = value });
    }

    // ---- Startup -----------------------------------------------------------

    public bool RunAtStartup
    {
        get => _runAtStartup;
        set
        {
            // Persist what the registry write actually achieved, not what was
            // asked for - a locked-down machine can refuse it, and the checkbox
            // shouldn't then claim an autostart entry that doesn't exist.
            var achieved = StartupService.SetEnabled(value);
            if (SetProperty(ref _runAtStartup, achieved))
            {
                _settings.SetRunAtStartup(achieved);
            }
            else if (achieved != value)
            {
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Unsubscribes from the settings service. The window is rebuilt each
    /// time it's opened, so without this every open would leave a live handler on
    /// a service that outlives it.</summary>
    public void Detach() => _settings.Changed -= OnSettingsChanged;

    private void SetUnits(UnitPreferences units)
    {
        _settings.SetUnits(units);
        RaiseUnitProperties();
    }

    private void SetTuning(WidgetTuning tuning)
    {
        _settings.SetTuning(tuning);
        RaiseTuningProperties();
    }

    private void OnSettingsChanged(object? sender, OverlaySettings settings)
    {
        // Guard against the re-entrancy of our own writes: a setter calls the
        // service, which raises this, which would raise the same properties again.
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            foreach (var widget in Widgets)
            {
                widget.Refresh(settings);
            }

            RaiseUnitProperties();
            RaiseTuningProperties();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RaiseUnitProperties()
    {
        OnPropertyChanged(nameof(FuelInLiters));
        OnPropertyChanged(nameof(FuelInGallons));
        OnPropertyChanged(nameof(TemperatureInCelsius));
        OnPropertyChanged(nameof(TemperatureInFahrenheit));
        OnPropertyChanged(nameof(SpeedInKph));
        OnPropertyChanged(nameof(SpeedInMph));
    }

    private void RaiseTuningProperties()
    {
        // The service sanitizes, so a value typed outside its band comes back
        // clamped - these notifications are what pull the clamped value into the UI.
        OnPropertyChanged(nameof(FuelSafetyMarginLaps));
        OnPropertyChanged(nameof(SetupFlashSeconds));
        OnPropertyChanged(nameof(RadarRangeMeters));
        OnPropertyChanged(nameof(RelativeSlotsPerSide));
        OnPropertyChanged(nameof(StandingsMaxPerClass));
    }
}
