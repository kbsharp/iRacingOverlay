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

        foreach (var preset in LayoutGuard.ScalePresets)
        {
            ScaleOptions.Add(preset);
        }

        OfferCurrentScale();
    }

    public string DisplayName { get; }

    /// <summary>
    /// The scales offered for this widget: the shared presets, plus this widget's
    /// current size when a corner-grip drag has left it somewhere in between. Without
    /// that extra entry a dragged widget would show an empty box here - the list
    /// would be claiming a size the widget doesn't have.
    /// </summary>
    public ObservableCollection<double> ScaleOptions { get; } = [];

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
    /// the same widget, a corner-grip drag, or Reset). Assigns the backing fields
    /// directly so it doesn't write back and loop.</summary>
    public void Refresh(OverlaySettings settings)
    {
        SetProperty(ref _isEnabled, settings.IsWidgetEnabled(_id), nameof(IsEnabled));
        SetProperty(ref _isClickThrough, settings.IsClickThrough(_id), nameof(IsClickThrough));

        var scale = settings.ScaleFor(_id);
        if (scale.Equals(_scale))
        {
            return;
        }

        // The list is updated before the notification, so the box has the new size
        // to select by the time it goes looking for it.
        _scale = scale;
        OfferCurrentScale();
        OnPropertyChanged(nameof(Scale));
    }

    /// <summary>
    /// Keeps the widget's current size in the dropdown when a grip drag has left it
    /// between presets. At most one non-preset entry is ever in the list - the one
    /// from a previous drag goes first, so abandoned sizes don't pile up.
    /// </summary>
    private void OfferCurrentScale()
    {
        for (var i = ScaleOptions.Count - 1; i >= 0; i--)
        {
            if (!LayoutGuard.ScalePresets.Contains(ScaleOptions[i]))
            {
                ScaleOptions.RemoveAt(i);
            }
        }

        if (LayoutGuard.ScalePresets.Contains(_scale))
        {
            return;
        }

        var index = 0;
        while (index < ScaleOptions.Count && ScaleOptions[index] < _scale)
        {
            index++;
        }

        ScaleOptions.Insert(index, _scale);
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

    /// <summary>Whether widgets stay hidden until iRacing is running. Reads
    /// straight through to the settings service - unlike <see cref="RunAtStartup"/>
    /// there's no external system that can refuse the change.</summary>
    public bool HideWhenSimClosed
    {
        get => _settings.Current.HideWhenSimClosed;
        set
        {
            _settings.SetHideWhenSimClosed(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the colour-blind-friendly palette is on. Repaints every
    /// meaning-hue live through the settings service - see
    /// <see cref="OverlaySettings.ColorBlindPalette"/>.</summary>
    public bool ColorBlindPalette
    {
        get => _settings.Current.ColorBlindPalette;
        set
        {
            _settings.SetColorBlindPalette(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the fuel widget shows the loaded setup and pulses at the
    /// start of a Qualify/Race. The flash length below only matters while this is
    /// on.</summary>
    public bool ShowSetupReminder
    {
        get => _settings.Current.ShowSetupReminder;
        set
        {
            _settings.SetShowSetupReminder(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the standings carries the manufacturer badge column.
    /// Experimental while the mark set is incomplete - see
    /// <see cref="OverlaySettings.ShowManufacturerBadges"/>.</summary>
    public bool ShowManufacturerBadges
    {
        get => _settings.Current.ShowManufacturerBadges;
        set
        {
            _settings.SetShowManufacturerBadges(value);
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the relative carries the catch/defend trend column.
    /// Experimental while the readout still has to be explained before it reads -
    /// see <see cref="OverlaySettings.ShowPaceTrend"/>.</summary>
    public bool ShowPaceTrend
    {
        get => _settings.Current.ShowPaceTrend;
        set
        {
            _settings.SetShowPaceTrend(value);
            OnPropertyChanged();
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
            OnPropertyChanged(nameof(ShowSetupReminder));
            OnPropertyChanged(nameof(ShowManufacturerBadges));
            OnPropertyChanged(nameof(ShowPaceTrend));
            // Also driven from the tray, so keep the checkbox in step with it.
            OnPropertyChanged(nameof(ColorBlindPalette));
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
