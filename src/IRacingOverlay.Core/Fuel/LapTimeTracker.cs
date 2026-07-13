namespace IRacingOverlay.Core.Fuel;

/// <summary>
/// Rolling average lap time, measured from session time at each lap change.
/// Feed every telemetry frame via <see cref="Update"/>; returns null until at
/// least one full lap has been measured. Not thread-safe - call from a single
/// thread. Mirrors <see cref="FuelCalculator"/>'s lap-detection approach.
/// </summary>
public sealed class LapTimeTracker
{
    private readonly int _windowSize;
    private readonly Queue<double> _lapTimes = new();

    private int? _currentLap;
    private double _lapStartTime;
    private double? _lastLapTime;

    public LapTimeTracker(int windowSize = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSize, 1);
        _windowSize = windowSize;
    }

    public double? AverageLapTimeSeconds => _lapTimes.Count > 0 ? _lapTimes.Average() : null;

    public double? LastLapTimeSeconds => _lastLapTime;

    public void Update(int lap, double sessionTimeSeconds)
    {
        if (_currentLap is null || lap < _currentLap)
        {
            // First frame, or the lap counter went backwards (tow / restart).
            BeginLap(lap, sessionTimeSeconds);
        }
        else if (lap == _currentLap + 1)
        {
            var lapTime = sessionTimeSeconds - _lapStartTime;
            if (lapTime > 0)
            {
                Record(lapTime);
            }

            BeginLap(lap, sessionTimeSeconds);
        }
        else if (lap > _currentLap)
        {
            // Lap counter jumped by more than one: the interval isn't a single
            // lap, so re-baseline without recording it.
            BeginLap(lap, sessionTimeSeconds);
        }
    }

    public void Reset()
    {
        _lapTimes.Clear();
        _lastLapTime = null;
        _currentLap = null;
    }

    private void BeginLap(int lap, double sessionTimeSeconds)
    {
        _currentLap = lap;
        _lapStartTime = sessionTimeSeconds;
    }

    private void Record(double lapTime)
    {
        _lapTimes.Enqueue(lapTime);
        _lastLapTime = lapTime;

        while (_lapTimes.Count > _windowSize)
        {
            _lapTimes.Dequeue();
        }
    }
}
