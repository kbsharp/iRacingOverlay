using IRacingOverlay.Core.Formatting;
using IRacingOverlay.Core.Radar;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Telemetry;
using IRSDKSharper;

namespace IRacingOverlay.Infrastructure.Telemetry;

/// <summary>
/// Live telemetry from the iRacing simulator via its shared-memory SDK
/// (wrapped by IRSDKSharper). Events are raised on SDK background threads.
/// </summary>
public sealed class IrsdkTelemetrySource : ITelemetrySource
{
    private const int MaxCars = 64;

    private readonly IRacingSdk _sdk = new();

    // Reused buffers for the CarIdx* arrays - no per-frame allocations here.
    private readonly int[] _carLap = new int[MaxCars];
    private readonly float[] _carLapDistPct = new float[MaxCars];
    private readonly float[] _carEstTime = new float[MaxCars];
    private readonly bool[] _carOnPitRoad = new bool[MaxCars];
    private readonly int[] _carTrackSurface = new int[MaxCars];
    private readonly int[] _carPosition = new int[MaxCars];
    private readonly int[] _carClassPosition = new int[MaxCars];
    private readonly int[] _carLapsCompleted = new int[MaxCars];
    private readonly float[] _carBestLap = new float[MaxCars];
    private readonly float[] _carLastLap = new float[MaxCars];
    private readonly float[] _carF2Time = new float[MaxCars];

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<SessionMetadata>? SessionMetadataReceived;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public IrsdkTelemetrySource()
    {
        // A safe default until the app pushes the persisted rate; the sim's own
        // 60 Hz feed is throttled to this many frames per delivered snapshot.
        _sdk.UpdateInterval = TelemetryRefresh.FramesPerUpdate(TelemetryRefresh.DefaultHz);
        _sdk.OnConnected += HandleConnected;
        _sdk.OnDisconnected += HandleDisconnected;
        _sdk.OnSessionInfo += HandleSessionInfo;
        _sdk.OnTelemetryData += HandleTelemetryData;
        _sdk.OnException += HandleException;
    }

    public void Start() => _sdk.Start();

    /// <summary>Retunes the poll rate live. IRSDKSharper reads UpdateInterval on
    /// every frame of its own loop, so a change here takes effect on the next one
    /// whether the SDK is started or not - no restart, no dropped connection.</summary>
    public void SetRefreshRateHz(int hz) => _sdk.UpdateInterval = TelemetryRefresh.FramesPerUpdate(hz);

    public void Stop()
    {
        if (_sdk.IsStarted)
        {
            _sdk.Stop();
        }
    }

    public void Dispose() => Stop();

    private void HandleConnected() => ConnectionChanged?.Invoke(this, true);

    private void HandleDisconnected() => ConnectionChanged?.Invoke(this, false);

    private void HandleException(Exception exception) => ErrorOccurred?.Invoke(this, exception);

    private void HandleSessionInfo()
    {
        var info = _sdk.Data.SessionInfo;

        var drivers = new Dictionary<int, RosterDriver>();
        foreach (var driver in info.DriverInfo?.Drivers ?? [])
        {
            if (driver.IsSpectator != 0 || driver.CarIsPaceCar != 0)
            {
                continue;
            }

            drivers[driver.CarIdx] = new RosterDriver(
                driver.CarIdx,
                driver.UserName ?? $"Car {driver.CarIdx}",
                driver.CarNumber ?? string.Empty,
                driver.IRating,
                driver.LicString ?? string.Empty,
                driver.CarClassEstLapTime,
                driver.CarClassShortName ?? string.Empty,
                driver.CarClassColor,
                driver.CarPath ?? string.Empty);
        }

        var sessionTypes = new Dictionary<int, string>();
        var sessionLaps = new Dictionary<int, int>();
        foreach (var session in info.SessionInfo?.Sessions ?? [])
        {
            sessionTypes[session.SessionNum] = session.SessionType ?? string.Empty;

            // SessionLaps is "unlimited" for timed sessions - only record real counts.
            if (SessionFormat.ParseLimit(session.SessionLaps) is { } laps)
            {
                sessionLaps[session.SessionNum] = laps;
            }
        }

        var incidentLimit = SessionFormat.ParseLimit(info.WeekendInfo?.WeekendOptions?.IncidentLimit);

        var setupName = info.DriverInfo?.DriverSetupName ?? string.Empty;
        var setupIsModified = (info.DriverInfo?.DriverSetupIsModified ?? 0) != 0;
        var trackLengthMeters = TrackLengthParser.ParseToMeters(info.WeekendInfo?.TrackLength);

        // Usable capacity, not the physical tank: series rules routinely cap
        // max fuel below the car's real tank (DriverCarMaxFuelPct), and the
        // gauge has to be scaled to what the driver can actually load. Both
        // vars are absent on older builds, so a missing/zero value falls back
        // to 0 and hides the gauge.
        var tankLiters = info.DriverInfo?.DriverCarFuelMaxLtr ?? 0f;
        var maxFuelPct = info.DriverInfo?.DriverCarMaxFuelPct ?? 0f;
        var tankCapacityLiters = tankLiters > 0 && maxFuelPct > 0 ? tankLiters * maxFuelPct : 0d;

        var sectorStartPcts = ParseSectorStartPcts(info);

        SessionMetadataReceived?.Invoke(
            this,
            new SessionMetadata(
                drivers, sessionTypes, setupName, setupIsModified, trackLengthMeters, incidentLimit,
                sessionLaps, tankCapacityLiters, sectorStartPcts));
    }

    /// <summary>
    /// The lap fraction each timing sector begins at, from the sim's
    /// SplitTimeInfo. Absent on some builds/sessions, where it stays null and the
    /// traffic forecast simply names no sector rather than guessing one.
    /// </summary>
    private static IReadOnlyList<double>? ParseSectorStartPcts(IRacingSdkSessionInfo info)
    {
        var sectors = info.SplitTimeInfo?.Sectors;
        if (sectors is null || sectors.Count == 0)
        {
            return null;
        }

        var starts = new List<double>(sectors.Count);
        foreach (var sector in sectors)
        {
            starts.Add(sector.SectorStartPct);
        }

        return starts;
    }

    private void HandleTelemetryData()
    {
        var data = _sdk.Data;

        data.GetIntArray("CarIdxLap", _carLap, 0, MaxCars);
        data.GetFloatArray("CarIdxLapDistPct", _carLapDistPct, 0, MaxCars);
        data.GetFloatArray("CarIdxEstTime", _carEstTime, 0, MaxCars);
        data.GetBoolArray("CarIdxOnPitRoad", _carOnPitRoad, 0, MaxCars);
        data.GetIntArray("CarIdxTrackSurface", _carTrackSurface, 0, MaxCars);
        data.GetIntArray("CarIdxPosition", _carPosition, 0, MaxCars);
        // Standings arrays - guarded, since not every build/session exposes them all.
        ReadIntArray(data, "CarIdxClassPosition", _carClassPosition);
        ReadIntArray(data, "CarIdxLapCompleted", _carLapsCompleted);
        ReadFloatArray(data, "CarIdxBestLapTime", _carBestLap);
        ReadFloatArray(data, "CarIdxLastLapTime", _carLastLap);
        ReadFloatArray(data, "CarIdxF2Time", _carF2Time);

        var cars = new List<CarTelemetry>();
        for (var i = 0; i < MaxCars; i++)
        {
            if (_carTrackSurface[i] == (int)CarTrackSurface.NotInWorld)
            {
                continue;
            }

            cars.Add(new CarTelemetry(
                i,
                _carLap[i],
                _carLapDistPct[i],
                _carEstTime[i],
                _carOnPitRoad[i],
                (CarTrackSurface)_carTrackSurface[i],
                _carPosition[i],
                _carClassPosition[i],
                _carLapsCompleted[i],
                _carBestLap[i],
                _carLastLap[i],
                _carF2Time[i]));
        }

        TelemetryReceived?.Invoke(this, new TelemetrySnapshot
        {
            SessionTimeSeconds = data.GetDouble("SessionTime"),
            SessionNum = data.GetInt("SessionNum"),
            SessionTimeRemainSeconds = data.GetDouble("SessionTimeRemain"),
            SessionLapsRemain = GetIntOrDefault(data, "SessionLapsRemainEx", -1),
            Lap = data.GetInt("Lap"),
            FuelLevelLiters = data.GetFloat("FuelLevel"),
            SpeedMetersPerSecond = data.GetFloat("Speed"),
            Gear = data.GetInt("Gear"),
            IsOnTrack = data.GetBool("IsOnTrack"),
            PlayerCarIdx = data.GetInt("PlayerCarIdx"),
            AirTempC = GetFloatOrDefault(data, "AirTemp"),
            TrackTempC = GetFloatOrDefault(data, "TrackTempCrew"),
            // TrackWetness only exists on builds with rain; default to Unknown.
            Wetness = (TrackWetness)GetIntOrDefault(data, "TrackWetness"),
            BrakeBiasPct = GetFloatOrDefault(data, "dcBrakeBias"),
            IncidentCount = GetIntOrDefault(data, "PlayerCarMyIncidentCount"),
            Flags = (SessionFlags)GetBitFieldOrDefault(data, "SessionFlags"),
            CarLeftRight = (CarLeftRight)GetIntOrDefault(data, "CarLeftRight"),
            PlayerYawRad = GetFloatOrDefault(data, "Yaw"),
            // The sim's own delta to the driver's best lap, plus the validity flag
            // it ships alongside. Both are absent on older builds, where the pair
            // degrades to "0, not valid" and the delta widget shows nothing.
            LapDeltaToBestSeconds = GetFloatOrDefault(data, "LapDeltaToBestLap"),
            LapDeltaToBestValid = GetBoolOrDefault(data, "LapDeltaToBestLap_OK"),
            Cars = cars,
        });
    }

    private static int GetIntOrDefault(IRacingSdkData data, string name, int fallback = 0) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetInt(datum) : fallback;

    private static float GetFloatOrDefault(IRacingSdkData data, string name, float fallback = 0f) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetFloat(datum) : fallback;

    private static bool GetBoolOrDefault(IRacingSdkData data, string name, bool fallback = false) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetBool(datum) : fallback;

    private static uint GetBitFieldOrDefault(IRacingSdkData data, string name, uint fallback = 0) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetBitField(datum) : fallback;

    private void ReadIntArray(IRacingSdkData data, string name, int[] buffer)
    {
        if (data.TelemetryDataProperties.ContainsKey(name))
        {
            data.GetIntArray(name, buffer, 0, MaxCars);
        }
        else
        {
            Array.Clear(buffer);
        }
    }

    private void ReadFloatArray(IRacingSdkData data, string name, float[] buffer)
    {
        if (data.TelemetryDataProperties.ContainsKey(name))
        {
            data.GetFloatArray(name, buffer, 0, MaxCars);
        }
        else
        {
            Array.Clear(buffer);
        }
    }
}
