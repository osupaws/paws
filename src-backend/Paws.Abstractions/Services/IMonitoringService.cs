using System;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for monitoring the game process and active client state.
/// </summary>
public interface IMonitoringService
{
    HostState CurrentState { get; }
    
    // Triggered when the host state changes (e.g. game started/stopped)
    event EventHandler<HostState> StateChanged;
    
    void StartMonitoring();
    void StopMonitoring();
}
