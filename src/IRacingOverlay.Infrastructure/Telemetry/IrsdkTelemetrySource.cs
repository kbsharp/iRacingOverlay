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
    // iRacing broadcasts 60 data frames per second; processing every 4th
    // (~15 Hz) is plenty for the overlay and keeps CPU usage negligible.
    private const int FramesPerUpdate = 4;

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
        _sdk.UpdateInterval = FramesPerUpdate;
        _sdk.OnConnected += HandleConnected;
        _sdk.OnDisconnected += HandleDisconnected;
        _sdk.OnSessionInfo += HandleSessionInfo;
        _sdk.OnTelemetryData += HandleTelemetryData;
        _sdk.OnException += HandleException;
    }

    public void Start() => _sdk.Start();

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
        foreach (var session in info.SessionInfo?.Sessions ?? [])
        {
            sessionTypes[session.SessionNum] = session.SessionType ?? string.Empty;
        }

        var setupName = info.DriverInfo?.DriverSetupName ?? string.Empty;
        var setupIsModified = (info.DriverInfo?.DriverSetupIsModified ?? 0) != 0;
        var trackLengthMeters = TrackLengthParser.ParseToMeters(info.WeekendInfo?.TrackLength);

        SessionMetadataReceived?.Invoke(
            this, new SessionMetadata(drivers, sessionTypes, setupName, setupIsModified, trackLengthMeters));
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
            CarLeftRight = (CarLeftRight)GetIntOrDefault(data, "CarLeftRight"),
            PlayerYawRad = GetFloatOrDefault(data, "Yaw"),
            Cars = cars,
        });
    }

    private static int GetIntOrDefault(IRacingSdkData data, string name, int fallback = 0) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetInt(datum) : fallback;

    private static float GetFloatOrDefault(IRacingSdkData data, string name, float fallback = 0f) =>
        data.TelemetryDataProperties.TryGetValue(name, out var datum) ? data.GetFloat(datum) : fallback;

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
