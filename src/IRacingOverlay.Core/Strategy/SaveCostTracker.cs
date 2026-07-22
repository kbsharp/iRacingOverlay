namespace IRacingOverlay.Core.Strategy;

/// <summary>
/// Learns what fuel saving costs in lap time, by watching the player's own laps.
///
/// The sim will never say this: how much a lift-and-coast costs depends on the
/// car, the track, the tyres and the driver. It is not a constant to look up, so
/// - like the pit loss - it is measured. Every completed clean lap contributes
/// one point of (litres burned, lap time), and the slope of a least-squares line
/// through them is the exchange rate: seconds of lap time per litre per lap.
///
/// The sign matters and is checked rather than assumed. Burning more fuel means
/// driving harder, so lap time should fall as litres rise. A fit that comes out
/// the other way is noise winning over signal, and the tracker reports nothing
/// instead - the same choice the pit-loss tracker makes before it has seen three
/// stops. Nothing here is shown until the laps actually say something.
/// </summary>
public sealed class SaveCostTracker
{
    /// <summary>A mid-lap fuel gain above this is a refuel, which makes the lap's
    /// burn meaningless. Same threshold as <see cref="Fuel.FuelCalculator"/>.</summary>
    private const float RefuelThresholdLiters = 0.2f;

    /// <summary>Only the recent past is this driver's current pace: tyres go off,
    /// the track rubbers in, and a stint's worth of laps is what a decision made
    /// now should rest on.</summary>
    private const int MaxSamples = 12;

    /// <summary>A slope through four points is a coincidence. Six clean laps is
    /// the smallest sample worth quoting a rate from.</summary>
    private const int MinSamples = 6;

    /// <summary>Laps slower than this multiple of the window's best are traffic, a
    /// mistake or a spin. They burn less fuel *and* lose time, which is the exact
    /// opposite of the relationship being measured - one such lap can flip the
    /// slope's sign on its own.
    ///
    /// It doubles as the bound on how steep a reported rate can get: no kept lap
    /// is more than 5% off the best, so the fit has only that much lap time to
    /// spread across a burn range that <see cref="MinSpreadFraction"/> forces to be
    /// real. A wild exchange rate has nowhere to come from.</summary>
    private const double OutlierLapTimeFactor = 1.05;

    /// <summary>The fitted range has to be a real range. Below this fraction of the
    /// mean burn, every lap was driven the same way and the slope is lap-time noise
    /// divided by almost nothing.</summary>
    private const double MinSpreadFraction = 0.06;

    private readonly Queue<Lap> _laps = new();

    private int? _currentLap;
    private float _fuelAtLapStart;
    private float _lastFuelLevel;
    private double _lapStartTime;
    private bool _currentLapInvalidated;

    /// <summary>Clean laps currently in the window.</summary>
    public int SampleCount => _laps.Count;

    /// <summary>
    /// The exchange rate the laps support, or <see cref="SaveCostEstimate.None"/>
    /// when they don't support one. Recomputed on demand - the window is a dozen
    /// points, and the filtering below depends on the window's own best lap, which
    /// changes as laps age out.
    /// </summary>
    public SaveCostEstimate Cost => Fit();

    /// <summary>Forgets every lap. A different session is a different car, fuel
    /// load and track state.</summary>
    public void Reset()
    {
        _laps.Clear();
        _currentLap = null;
        _currentLapInvalidated = false;
    }

    /// <summary>
    /// Records this frame. Lap detection mirrors <see cref="Fuel.FuelCalculator"/>:
    /// a lap counts only when it was a single clean step with no refuel. This one
    /// also drops any lap that touched pit road, because an in- or out-lap is
    /// neither a normal burn nor a normal lap time.
    /// </summary>
    public void Update(int lap, float fuelLevelLiters, double sessionTimeSeconds, bool onPitRoad)
    {
        if (_currentLap is null || lap < _currentLap)
        {
            // First frame, or the lap counter went backwards (tow / restart).
            BeginLap(lap, fuelLevelLiters, sessionTimeSeconds, onPitRoad);
        }
        else if (lap == _currentLap)
        {
            if (onPitRoad || fuelLevelLiters > _lastFuelLevel + RefuelThresholdLiters)
            {
                _currentLapInvalidated = true;
            }
        }
        else
        {
            var usage = (double)_fuelAtLapStart - fuelLevelLiters;
            var lapTime = sessionTimeSeconds - _lapStartTime;

            if (lap == _currentLap + 1 && !_currentLapInvalidated && usage > 0 && lapTime > 0)
            {
                Record(usage, lapTime);
            }

            BeginLap(lap, fuelLevelLiters, sessionTimeSeconds, onPitRoad);
        }

        _lastFuelLevel = fuelLevelLiters;
    }

    private void BeginLap(int lap, float fuelLevelLiters, double sessionTimeSeconds, bool onPitRoad)
    {
        _currentLap = lap;
        _fuelAtLapStart = fuelLevelLiters;
        _lapStartTime = sessionTimeSeconds;
        _currentLapInvalidated = onPitRoad;
    }

    private void Record(double liters, double seconds)
    {
        _laps.Enqueue(new Lap(liters, seconds));

        while (_laps.Count > MaxSamples)
        {
            _laps.Dequeue();
        }
    }

    /// <summary>
    /// Least squares through the clean laps in the window, with every guard that
    /// stands between a slope and a number worth showing.
    /// </summary>
    private SaveCostEstimate Fit()
    {
        if (_laps.Count < MinSamples)
        {
            return SaveCostEstimate.None;
        }

        var bestLapTime = double.MaxValue;
        foreach (var lap in _laps)
        {
            bestLapTime = Math.Min(bestLapTime, lap.Seconds);
        }

        var cutoff = bestLapTime * OutlierLapTimeFactor;

        var count = 0;
        double sumLiters = 0, sumSeconds = 0;
        var minLiters = double.MaxValue;
        var maxLiters = double.MinValue;

        foreach (var lap in _laps)
        {
            if (lap.Seconds > cutoff)
            {
                continue;
            }

            count++;
            sumLiters += lap.Liters;
            sumSeconds += lap.Seconds;
            minLiters = Math.Min(minLiters, lap.Liters);
            maxLiters = Math.Max(maxLiters, lap.Liters);
        }

        if (count < MinSamples)
        {
            return SaveCostEstimate.None;
        }

        var meanLiters = sumLiters / count;
        var meanSeconds = sumSeconds / count;
        var spread = maxLiters - minLiters;

        if (meanLiters <= 0 || spread < meanLiters * MinSpreadFraction)
        {
            return SaveCostEstimate.None;
        }

        double covariance = 0, variance = 0;
        foreach (var lap in _laps)
        {
            if (lap.Seconds > cutoff)
            {
                continue;
            }

            var dx = lap.Liters - meanLiters;
            covariance += dx * (lap.Seconds - meanSeconds);
            variance += dx * dx;
        }

        if (variance <= 0)
        {
            return SaveCostEstimate.None;
        }

        // Lap time against litres: burning more should be quicker, so a usable fit
        // slopes down. Anything else is noise, and noise doesn't get a readout.
        var secondsPerLiter = -(covariance / variance);
        if (secondsPerLiter <= 0)
        {
            return SaveCostEstimate.None;
        }

        return new SaveCostEstimate(secondsPerLiter, minLiters, spread);
    }

    /// <summary>One clean lap: what it burned and how long it took.</summary>
    private readonly record struct Lap(double Liters, double Seconds);
}
