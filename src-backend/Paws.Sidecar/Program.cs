using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;
using Paws.Core.Services;
using Paws.Core.Plugins;
using Paws.Core.Storage;
using Paws.Core.Monitoring;
using Realms;

namespace Paws.Sidecar;

/// <summary>
/// Main entry point for the Paws Sidecar process.
/// Handles DI setup, STDIN command loop, and RPC routing.
/// </summary>
class Program
{
    private static IServiceProvider _serviceProvider = null!;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static async Task Main(string[] args)
    {
        // CRITICAL for Tauri: Forced STDOUT buffer flush.
        // Otherwise, console logs (sitting in the pipe) accumulate and don't reach the Rust parser 
        // until the buffer fills up (usually 4KB).
        var stdoutWriter = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdoutWriter);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            Console.WriteLine($"[Sidecar] Starting... CWD is {Environment.CurrentDirectory}");
            // 1. Dependency Injection Setup
            var services = new ServiceCollection();

            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IScopeManager, ScopeManager>();
            services.AddSingleton<IStorageService, StorageService>();
            services.AddSingleton<IMonitoringService, MonitoringService>();
            services.AddSingleton<IVfsService, VfsService>();
            services.AddSingleton<IGameDataService, GameDataService>();
            services.AddSingleton<IPluginManager, Paws.Core.Plugins.PluginManager>();

            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IOsuAuthService, OsuAuthService>();
            services.AddScoped<IThemeService, ThemeService>();
            services.AddScoped<IPackageImportService, PackageImportService>();

            // RPC Handlers
            services.AddScoped<Paws.Core.Rpc.IRpcHandler, Paws.Core.Rpc.SystemHandler>();
            services.AddScoped<Paws.Core.Rpc.IRpcHandler, Paws.Core.Rpc.GameHandler>();
            services.AddScoped<Paws.Core.Rpc.IRpcHandler, Paws.Core.Rpc.StorageHandler>();

            _serviceProvider = services.BuildServiceProvider();

            var dbSvc = _serviceProvider.GetRequiredService<IDatabaseService>();
            Console.WriteLine($"[Sidecar] Database: {dbSvc.DatabasePath}");

            // Initialize plugin loading
            var pluginManager = _serviceProvider.GetRequiredService<IPluginManager>();
            await pluginManager.LoadPluginsAsync();

            // Auto-restore Developer Mode after kernel restart
            using (var initScope = _serviceProvider.CreateScope())
            {
                var configSvc = initScope.ServiceProvider.GetRequiredService<IConfigService>();
                var initConfig = await configSvc.GetConfigAsync();
                if (initConfig.IsDeveloperModeEnabled && !string.IsNullOrWhiteSpace(initConfig.DevPluginPath))
                {
                    Console.WriteLine($"[Sidecar] Restoring DevPlugin from configuration...");
                    await pluginManager.LoadDevPluginAsync(initConfig.DevPluginPath);
                }
            }

            Console.WriteLine($"[Sidecar] DI Built in {Environment.CurrentDirectory}. Waiting for commands...");

            // 2. Command Processing Loop (stdin)
            while (true)
            {
                var input = await Console.In.ReadLineAsync();
                if (input == null)
                {
                    Console.WriteLine("[Sidecar] STDIN is closed by the parent, exiting loop.");
                    break;
                }
                if (string.IsNullOrWhiteSpace(input)) continue;

                // Process commands concurrently to prevent long-running tasks (like waitForOsuCallback) 
                // from blocking the stdin reader loop.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var command = JsonSerializer.Deserialize<SidecarCommand>(input, _jsonOptions);
                        if (command == null) return;

                        Console.WriteLine($"[Sidecar] Input: {input}");
                        
                        var result = await HandleCommand(command);
                        result.RequestId = command.RequestId;
                        Console.WriteLine(JsonSerializer.Serialize(result, _jsonOptions));
                    }
                    catch (Exception ex)
                    {
                        var error = new SidecarResponse { Success = false, Error = ex.Message };
                        Console.WriteLine(JsonSerializer.Serialize(error, _jsonOptions));
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sidecar] FATAL ERROR IN MAIN: {ex}");
            Console.Error.WriteLine($"[Sidecar] FATAL ERROR IN MAIN: {ex}");
            File.WriteAllText("paws-crash.log", $"[{DateTime.Now}] MAIN EXCEPTION:\n{ex}");
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        File.WriteAllText("paws-crash-unhandled.log", $"[{DateTime.Now}] UNHANDLED EXCEPTION:\n{e.ExceptionObject}");
    }

    private static async Task<SidecarResponse> HandleCommand(SidecarCommand command)
    {
        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<Paws.Core.Rpc.IRpcHandler>();
        var pluginManager = scope.ServiceProvider.GetRequiredService<IPluginManager>();

        var handler = handlers.FirstOrDefault(h => h.CanHandle(command.Action));
        if (handler == null)
            return new SidecarResponse { Success = false, Error = $"Unknown action: {command.Action}" };

        // 1. Scope Validation
        if (command.CallerId != "host")
        {
            var manifest = pluginManager.GetManifest(command.CallerId);
            if (manifest == null)
                return new SidecarResponse { Success = false, Error = $"Access Denied: Unknown plugin '{command.CallerId}'" };

            if (!manifest.IsSystem)
            {
                var requiredScope = handler.GetRequiredScope(command.Action);
                if (requiredScope != null && !manifest.Scopes.Contains(requiredScope))
                {
                    return new SidecarResponse { Success = false, Error = $"Access Denied: Plugin '{command.CallerId}' missing required scope '{requiredScope}'" };
                }

                if (handler.IsHostOnly(command.Action))
                {
                    return new SidecarResponse { Success = false, Error = $"Access Denied: Action '{command.Action}' is restricted to host only" };
                }
            }
        }

        // 2. Execution
        try
        {
            var data = await handler.HandleAsync(command.Action, command.CallerId, command.Params);
            return new SidecarResponse { Success = true, Data = data };
        }
        catch (Exception ex)
        {
            return new SidecarResponse { Success = false, Error = ex.Message };
        }
    }
}

public class SidecarCommand
{
    public string RequestId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string CallerId { get; set; } = "host";
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}

public class SidecarResponse
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public object? Data { get; set; }
    public string? Error { get; set; }
}
