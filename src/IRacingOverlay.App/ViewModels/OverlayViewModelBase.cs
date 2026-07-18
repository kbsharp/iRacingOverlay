using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using IRacingOverlay.Core.Session;
using IRacingOverlay.Core.Settings;
using IRacingOverlay.Core.Telemetry;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Shared connection-state handling for widget view models, plus the telemetry
/// surface the composition root fans out to. Declaring
/// <see cref="ApplyTelemetry"/> and friends here is what lets <see cref="App"/>
/// loop over its widget list instead of naming all five view models in four
/// separate event handlers.
///
/// Must only be touched from the UI thread; <see cref="App"/> marshals telemetry
/// events onto the dispatcher before calling in.
/// </summary>
public abstract class OverlayViewModelBase : ObservableObject
{
    private const string WaitingStatus = "Waiting for iRacing";

    private readonly string _connectedLabel;

    private string _connectionStatus = WaitingStatus;
    private bool _isConnected;

    protected OverlayViewModelBase(string connectedLabel)
    {
        _connectedLabel = connectedLabel;
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    public void SetConnectionState(bool connected)
    {
        IsConnected = connected;
        ConnectionStatus = connected ? _connectedLabel : WaitingStatus;
    }

    public void ReportError(Exception exception)
    {
        Debug.WriteLine(exception);
        ConnectionStatus = "Telemetry error";
    }

    /// <summary>Applies one telemetry frame. Called ~15Hz on the UI thread.</summary>
    public abstract void ApplyTelemetry(TelemetrySnapshot snapshot);

    /// <summary>Applies slow-changing roster/session data. Only fires when the sim
    /// re-broadcasts session info, so widgets that don't need it (fuel) leave the
    /// default no-op in place.</summary>
    public virtual void ApplySessionMetadata(SessionMetadata metadata)
    {
    }

    /// <summary>Applies changed user settings. The default reads nothing; widgets
    /// with tunable numbers or unit-dependent text override it and re-render, so
    /// a settings change lands without waiting for the next telemetry frame.</summary>
    public virtual void ApplySettings(OverlaySettings settings)
    {
    }
}
