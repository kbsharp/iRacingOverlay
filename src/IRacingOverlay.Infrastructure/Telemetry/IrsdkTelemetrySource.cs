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

    private readonly IRacingSdk _sdk = new();

    public event EventHandler<TelemetrySnapshot>? TelemetryReceived;
    public event EventHandler<bool>? ConnectionChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public IrsdkTelemetrySource()
    {
        _sdk.UpdateInterval = FramesPerUpdate;
        _sdk.OnConnected += HandleConnected;
        _sdk.OnDisconnected += HandleDisconnected;
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

    private void HandleTelemetryData()
    {
        var data = _sdk.Data;

        TelemetryReceived?.Invoke(this, new TelemetrySnapshot(
            SessionTimeSeconds: data.GetDouble("SessionTime"),
            Lap: data.GetInt("Lap"),
            FuelLevelLiters: data.GetFloat("FuelLevel"),
            SpeedMetersPerSecond: data.GetFloat("Speed"),
            Gear: data.GetInt("Gear"),
            IsOnTrack: data.GetBool("IsOnTrack")));
    }
}
