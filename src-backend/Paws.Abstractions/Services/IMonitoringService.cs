using System;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

public interface IMonitoringService
{
    HostState CurrentState { get; }
    
    // Событие, срабатывающее только при реальном изменении состояния
    event EventHandler<HostState> StateChanged;
    
    void StartMonitoring();
    void StopMonitoring();
}
