using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IRacingOverlay.App.ViewModels;

/// <summary>
/// Shared connection-state handling for widget view models. Must only be
/// touched from the UI thread; <see cref="App"/> marshals telemetry events
/// onto the dispatcher before calling in.
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
}
