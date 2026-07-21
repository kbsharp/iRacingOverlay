using IRacingOverlay.Core.Session;

namespace IRacingOverlay.Core.Telemetry;

/// <summary>
/// A live feed of telemetry snapshots. Implementations raise events on
/// background threads; consumers are responsible for marshalling to the
/// UI thread.
/// </summary>
public interface ITelemetrySource : IDisposable
{
    event EventHandler<TelemetrySnapshot>? TelemetryReceived;

    /// <summary>Raised when the sim (re)broadcasts session info: roster, session names.</summary>
    event EventHandler<SessionMetadata>? SessionMetadataReceived;

    /// <summary>Raised with true when the sim starts broadcasting, false when it exits.</summary>
    event EventHandler<bool>? ConnectionChanged;

    event EventHandler<Exception>? ErrorOccurred;

    void Start();

    void Stop();

    /// <summary>Sets how often telemetry snapshots are delivered, in hertz. The
    /// live source throttles the sim's 60 Hz broadcast to the nearest divisor; the
    /// demo source retimes its own tick. The rate is snapped to
    /// <see cref="TelemetryRefresh.AllowedHz"/>, and it's safe to call before or
    /// after <see cref="Start"/> — the change takes effect on the next frame.</summary>
    void SetRefreshRateHz(int hz);
}
