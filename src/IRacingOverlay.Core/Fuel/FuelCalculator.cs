namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// Tracks fuel burn across completed laps and produces a rolling estimate.
/// Feed it every telemetry frame via <see cref="Update"/>; lap changes are
/// detected internally. Not thread-safe - call from a single thread.
/// </summary>
public sealed class FuelCalculator
{
    /// <summary>A mid-lap fuel gain above this is treated as a refuel, which
    /// invalidates the current lap's burn measurement.</summary>
    private const float RefuelThresholdLiters = 0.2f;

    private readonly int _windowSize;
    private readonly Queue<double> _lapUsages = new();

    private int? _currentLap;
    private float _fuelAtLapStart;
    private float _lastFuelLevel;
    private bool _currentLapInvalidated;
    private double? _lastLapLiters;

    public FuelCalculator(int windowSize = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        _windowSize = windowSize;
    }

    public FuelEstimate Update(int lap, float fuelLevelLiters)
    {
        if (_currentLap is null || lap < _currentLap)
        {
            // First frame, or the lap counter went backwards (tow / session
            // restart): re-baseline, but keep the burn history - laps already
            // recorded are still representative of this car and track.
            BeginLap(lap, fuelLevelLiters);
        }
        else if (lap == _currentLap)
        {
            if (fuelLevelLiters > _lastFuelLevel + RefuelThresholdLiters)
            {
                _currentLapInvalidated = true;
            }
        }
        else
        {
            var usage = (double)_fuelAtLapStart - fuelLevelLiters;
            var isSingleLapStep = lap == _currentLap + 1;

            if (isSingleLapStep && !_currentLapInvalidated && usage > 0)
            {
                RecordLap(usage);
            }

            BeginLap(lap, fuelLevelLiters);
        }

        _lastFuelLevel = fuelLevelLiters;

        return BuildEstimate(fuelLevelLiters);
    }

    /// <summary>
    /// The estimate as of the last <see cref="Update"/>, recomputed from the
    /// existing history without advancing any lap-detection state.
    ///
    /// Exists so a caller can re-render (after a units change, say) without
    /// feeding the same frame through <see cref="Update"/> twice - which would
    /// look harmless but re-enters the refuel and lap-step detection.
    /// </summary>
    public FuelEstimate Current => BuildEstimate(_lastFuelLevel);

    public void Reset()
    {
        _lapUsages.Clear();
        _lastLapLiters = null;
        _currentLap = null;
        _currentLapInvalidated = false;
    }

    private void BeginLap(int lap, float fuelLevelLiters)
    {
        _currentLap = lap;
        _fuelAtLapStart = fuelLevelLiters;
        _currentLapInvalidated = false;
    }

    private void RecordLap(double usage)
    {
        _lapUsages.Enqueue(usage);
        _lastLapLiters = usage;

        while (_lapUsages.Count > _windowSize)
        {
            _lapUsages.Dequeue();
        }
    }

    private FuelEstimate BuildEstimate(float fuelLevelLiters)
    {
        if (_lapUsages.Count == 0)
        {
            return FuelEstimate.Empty;
        }

        var average = _lapUsages.Average();
        double? lapsRemaining = average > 0 ? fuelLevelLiters / average : null;

        return new FuelEstimate(average, _lastLapLiters, lapsRemaining, _lapUsages.Count);
    }
}
