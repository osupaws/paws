using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;

namespace Paws.Core.Monitoring;

/// <summary>
/// Monitors running game processes and updates the HostState.
/// Uses background polling to detect osu!stable and osu!lazer.
/// </summary>
public class MonitoringService : IMonitoringService, IDisposable
{
    private readonly IConfigService _config;
    private CancellationTokenSource? _cts;
    private HostState _currentState = new HostState();
    
    public HostState CurrentState => _currentState;
    public event EventHandler<HostState>? StateChanged;

    public MonitoringService(IConfigService config)
    {
        _config = config;
    }

    public void StartMonitoring()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        Task.Run(() => MonitorLoopAsync(_cts.Token));
        Console.WriteLine("[MonitoringService] Started background monitoring of osu! processes.");
    }

    public void StopMonitoring()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        Console.WriteLine("[MonitoringService] Stopped background monitoring.");
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var newState = CheckOsuProcesses();
                
                if (newState.HasChanged(_currentState))
                {
                    _currentState = newState;
                    StateChanged?.Invoke(this, _currentState);
                    Console.WriteLine($"[MonitoringService] State changed: Running={newState.IsOsuRunning}, Client={newState.ActiveClient}, PID={newState.ProcessId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonitoringService] Error during scan: {ex.Message}");
            }

            // Check every 3 seconds
            await Task.Delay(3000, ct);
        }
    }

    private HostState CheckOsuProcesses()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var processes = Process.GetProcessesByName(isWindows ? "osu!" : "osu");
        
        if (processes.Length == 0 && !isWindows)
        {
            processes = Process.GetProcessesByName("osu-lazer"); 
        }

        if (processes.Length == 0)
        {
            return new HostState { IsOsuRunning = false, ActiveClient = GameClientType.None, ProcessId = 0 };
        }

        var activeProc = processes.FirstOrDefault();
        if (activeProc == null) 
            return new HostState { IsOsuRunning = false, ActiveClient = GameClientType.None, ProcessId = 0 };

        var state = new HostState
        {
            IsOsuRunning = true,
            ProcessId = activeProc.Id
        };

        try
        {
            var path = activeProc.MainModule?.FileName;
            if (path != null)
            {
                if (!string.IsNullOrEmpty(_config.Config.LazerPath) && path.StartsWith(_config.Config.LazerPath, StringComparison.OrdinalIgnoreCase))
                {
                    state.ActiveClient = GameClientType.Lazer;
                }
                else if (path.Contains("osulazer", StringComparison.OrdinalIgnoreCase) || path.Contains("lazer", StringComparison.OrdinalIgnoreCase))
                {
                    state.ActiveClient = GameClientType.Lazer;
                }
                else
                {
                    state.ActiveClient = GameClientType.Stable;
                }
            }
            else
            {
                state.ActiveClient = GameClientType.Stable;
            }
        }
        catch
        {
            state.ActiveClient = GameClientType.Stable;
        }

        return state;
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
