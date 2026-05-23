using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Plugins;
using Paws.Abstractions.Services;

namespace Paws.Core.Plugins;

/// <summary>
/// Internal container for a loaded plugin instance and its runtime metadata.
/// </summary>
public class PluginInstance
{
    public PluginManifest Manifest { get; set; } = null!;
    public AssemblyLoadContext? LoadContext { get; set; }
    public Assembly? Assembly { get; set; }
    public IPawsPlugin? PluginRuntime { get; set; }
    public Dictionary<string, MethodInfo> ExportedMethods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Core implementation of IPluginManager. 
/// Handles assembly loading, hot-reload, and Cross-Plugin RPC.
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly string _pluginsDirectory;
    private readonly IStorageService _storage;
    private readonly IGameDataService _gameData;
    private readonly IMonitoringService _monitor;
    private readonly IScopeManager _scopeManager;
    private readonly IVfsService _vfs;

    private readonly ConcurrentDictionary<string, PluginInstance> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _devWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<object> _keepAlive = new(); // Strong references to prevent GC from collecting timers!

    public PluginManager(IDatabaseService database, IStorageService storage, IGameDataService gameData, IMonitoringService monitor, IScopeManager scopeManager, IVfsService vfs)
    {
        _storage = storage;
        _gameData = gameData;
        _monitor = monitor;
        _scopeManager = scopeManager;
        _vfs = vfs;
        
        _pluginsDirectory = database.PluginsDirectory;
    }

    public async Task LoadPluginsAsync()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
            return;
        }

        var pluginDirs = Directory.GetDirectories(_pluginsDirectory);
        foreach (var dir in pluginDirs)
        {
            await CoreLoadPluginAsync(dir);
        }
    }

    private async Task<PluginManifest?> CoreLoadPluginAsync(string dir)
    {
        var manifestPath = Path.Combine(dir, "plugin.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (manifest == null || string.IsNullOrEmpty(manifest.Id) || string.IsNullOrEmpty(manifest.EntryPoint))
                return null;

            // If plugin is already loaded (e.g. during Reload), unload it first
            if (_loadedPlugins.ContainsKey(manifest.Id))
            {
                await UnloadPluginAsync(manifest.Id);
            }

            // --- OPTIONAL DLL LOADING ---
            if (manifest.EntryPoint.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var dllPath = Path.Combine(dir, manifest.EntryPoint);
                if (!File.Exists(dllPath)) return null;

                // --- SECURITY SCAN ---
                var scannerResult = PluginSecurityScanner.Analyze(dllPath, manifest);
                if (!scannerResult.IsSafe)
                {
                    var reason = string.Join("; ", scannerResult.Violations);
                    Console.WriteLine($"[Security] Plugin {manifest.Id} is REJECTED: {reason}");
                    return null;
                }

                var alc = new PluginLoadContext(dllPath);
                using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var assembly = alc.LoadFromStream(fs);

                var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPawsPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                if (pluginType == null)
                {
                    Console.WriteLine($"[PluginManager] NO IPawsPlugin implementation found in {manifest.Id}");
                    return null;
                }

                var pluginObj = (IPawsPlugin)Activator.CreateInstance(pluginType)!;
                var hostApi = new HostApi(manifest.Id, _storage, _gameData, _monitor, _vfs, this);

                await pluginObj.InitializeAsync(hostApi);

                var instance = new PluginInstance
                {
                    Manifest = manifest,
                    LoadContext = alc,
                    Assembly = assembly,
                    PluginRuntime = pluginObj
                };

                foreach (var method in pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = method.GetCustomAttribute<PublicEntryPointAttribute>();
                    if (attr != null)
                    {
                        var name = string.IsNullOrEmpty(attr.MethodName) ? method.Name : attr.MethodName;
                        instance.ExportedMethods[name] = method;
                    }
                }

                _loadedPlugins[manifest.Id] = instance;
            }
            else
            {
                // UI-Only Plugin
                _loadedPlugins[manifest.Id] = new PluginInstance { Manifest = manifest };
                Console.WriteLine($"[PluginManager] UI-Only Plugin '{manifest.Name}' registered.");
            }

            // --- REGISTER SCOPES ---
            _scopeManager.RegisterPluginScopes(manifest.Id, manifest.Scopes);

            Console.WriteLine($"[PluginManager] {manifest.Name} v{manifest.Version} [{manifest.Id}] ready!");
            return manifest;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PluginManager] Failed to load plugin in {dir}: {ex.Message}");
            return null;
        }
    }

    public async Task LoadDevPluginAsync(string absolutePathToFolder)
    {
        var manifest = await CoreLoadPluginAsync(absolutePathToFolder);
        if (manifest == null) return;

        if (_devWatchers.ContainsKey(manifest.Id)) return;

        Console.WriteLine($"[Hotplug] Enabled Hot Reload for {manifest.Id} at {absolutePathToFolder}");
        var watcher = new FileSystemWatcher(absolutePathToFolder, "*.dll");

        System.Threading.Timer? debounceTimer = null;

        FileSystemEventHandler onDllChanged = (s, e) =>
        {
            if (e.Name == null || !e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return;

            // Debounce 500ms to protect against fragmented compilation (e.g. during dotnet build)
            debounceTimer?.Dispose();
            debounceTimer = new System.Threading.Timer(async _ =>
            {
                Console.WriteLine($"[Hotplug] Code change detected via {e.ChangeType}! Reloading: {manifest.Id}");
                await CoreLoadPluginAsync(absolutePathToFolder);
                Console.Out.Flush(); // Flush to stdout for Tauri/IPC pipe
            }, null, 500, System.Threading.Timeout.Infinite);
        };

        // Subscribe to all events as 'dotnet build' often uses Created/Renamed patterns
        watcher.Changed += onDllChanged;
        watcher.Created += onDllChanged;
        watcher.Renamed += (s, e) => onDllChanged(s, e);
        
        watcher.EnableRaisingEvents = true;
        
        _devWatchers[manifest.Id] = watcher;
        
        // Pin the delegate in memory to prevent GC collection
        _keepAlive.Add(onDllChanged);
    }

    public async Task UnloadPluginAsync(string pluginId)
    {
        if (_loadedPlugins.TryRemove(pluginId, out var instance))
        {
            try
            {
                Console.WriteLine($"[PluginManager] Shutting down plugin {pluginId}...");
                if (instance.PluginRuntime != null)
                {
                    await instance.PluginRuntime.ShutdownAsync();
                }
                
                if (instance.LoadContext != null)
                {
                    instance.LoadContext.Unload();
                    Console.WriteLine($"[PluginManager] ALC for {pluginId} successfully unloaded.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginManager] Error during unload of {pluginId}: {ex.Message}");
            }
        }
    }

    public IEnumerable<PluginManifest> GetLoadedPlugins() => _loadedPlugins.Values.Select(p => p.Manifest);

    public PluginManifest? GetManifest(string pluginId)
    {
        return _loadedPlugins.TryGetValue(pluginId, out var p) ? p.Manifest : null;
    }

    public Task<object?> InvokePluginMethodAsync(string sourcePluginId, string targetPluginId, string method, Dictionary<string, object>? args)
    {
        // 1. Zero Trust: Check caller's API Scopes
        if (!_scopeManager.HasScope(sourcePluginId, $"api:plugin:{targetPluginId}"))
        {
            throw new UnauthorizedAccessException($"[Sandbox] Plugin {sourcePluginId} missing scope api:plugin:{targetPluginId} to perform Cross-Plugin RPC!");
        }

        if (!_loadedPlugins.TryGetValue(targetPluginId, out var targetPlugin))
            throw new InvalidOperationException($"Target plugin {targetPluginId} is offline or missing.");

        if (targetPlugin.PluginRuntime == null)
            throw new InvalidOperationException($"Plugin {targetPluginId} is UI-only and has no backend methods.");

        if (!targetPlugin.ExportedMethods.TryGetValue(method, out var methodInfo))
            throw new MissingMethodException($"Plugin {targetPluginId} strongly denies access or missing method: {method}");

        // 2. Argument binding and Reflection call
        var parameters = methodInfo.GetParameters();
        object?[] invokeArgs = new object?[parameters.Length];

        if (args != null && parameters.Length > 0)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var pName = parameters[i].Name;
                if (pName != null && args.TryGetValue(pName, out var val))
                {
                    // TODO: Implement deep JSON casting for complex argument structures
                    invokeArgs[i] = val; 
                }
            }
        }

        try
        {
            var result = methodInfo.Invoke(targetPlugin.PluginRuntime, invokeArgs);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RPC] Error calling {targetPluginId}.{method}: {ex.InnerException?.Message ?? ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// AssemblyLoadContext that provides dependency isolation (Sandboxing) for plugins.
/// Prevents "Dependency Hell" by allowing plugins to load their own versions of shared libraries.
/// </summary>
class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // CRITICAL: Shared contracts (like Paws.Abstractions) must only be loaded once in the Kernel space.
        // Otherwise, typeof(IPawsPlugin) in kernel != typeof(IPawsPlugin) in plugin.
        if (assemblyName.Name == "Paws.Abstractions")
        {
            return null; // Returning null forces the CLR to use AssemblyLoadContext.Default (Kernel memory)
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }
        return IntPtr.Zero;
    }
}
