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

public class PluginInstance
{
    public PluginManifest Manifest { get; set; } = null!;
    public AssemblyLoadContext LoadContext { get; set; } = null!;
    public Assembly Assembly { get; set; } = null!;
    public IPawsPlugin PluginRuntime { get; set; } = null!;
    public Dictionary<string, MethodInfo> ExportedMethods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class PluginManager : IPluginManager
{
    private readonly string _pluginsDirectory;
    private readonly IStorageService _storage;
    private readonly IGameDataService _gameData;
    private readonly IMonitoringService _monitor;
    private readonly IScopeManager _scopeManager;

    private readonly ConcurrentDictionary<string, PluginInstance> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _devWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<object> _keepAlive = new(); // Жесткие ссылки, чтобы GC не убил таймеры!

    public PluginManager(IStorageService storage, IGameDataService gameData, IMonitoringService monitor, IScopeManager scopeManager)
    {
        _storage = storage;
        _gameData = gameData;
        _monitor = monitor;
        _scopeManager = scopeManager;
        
        _pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "Plugins");
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

            // Если плагин уже загружен (например, при Reload)
            if (_loadedPlugins.ContainsKey(manifest.Id))
            {
                await UnloadPluginAsync(manifest.Id);
            }

            var dllPath = Path.Combine(dir, manifest.EntryPoint);
            if (!File.Exists(dllPath)) return null;

            // --- ZERO-TRUST SECURITY SCAN ---
            var securityResult = PluginSecurityScanner.Analyze(dllPath, manifest);
            if (!securityResult.IsSafe)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[SECURITY] Plugin '{manifest.Id}' REJECTED! Missing 'unsafe' scope for illegal calls:");
                foreach (var violation in securityResult.Violations.Take(5))
                {
                    Console.WriteLine($"  -> {violation}");
                }
                if (securityResult.Violations.Count > 5) Console.WriteLine($"  ... and {securityResult.Violations.Count - 5} more.");
                Console.ResetColor();
                Console.Out.Flush();
                return null;
            }
            // --------------------------------

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
            var hostApi = new HostApi(manifest.Id, _storage, _gameData, _monitor, this);

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
            Console.WriteLine($"[PluginManager] {manifest.Name} v{manifest.Version} [{manifest.Id}] loaded!");
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

            // Debounce 500ms для защиты от рваной компиляции Visual Studio/dotnet build
            debounceTimer?.Dispose();
            debounceTimer = new System.Threading.Timer(async _ =>
            {
                Console.WriteLine($"[Hotplug] Code change detected via {e.ChangeType}! Reloading: {manifest.Id}");
                await CoreLoadPluginAsync(absolutePathToFolder);
                Console.Out.Flush(); // Проталкиваем строку в трубу Tauri
            }, null, 500, System.Threading.Timeout.Infinite);
        };

        // Подписываемся на ВСЕ события, так как dotnet build часто делает Created/Renamed
        watcher.Changed += onDllChanged;
        watcher.Created += onDllChanged;
        watcher.Renamed += (s, e) => onDllChanged(s, e);
        
        watcher.EnableRaisingEvents = true;
        
        _devWatchers[manifest.Id] = watcher;
        
        // Жестко фиксируем в памяти, чтобы GC не собрал делегат
        _keepAlive.Add(onDllChanged);
    }

    public async Task UnloadPluginAsync(string pluginId)
    {
        if (_loadedPlugins.TryRemove(pluginId, out var instance))
        {
            try
            {
                Console.WriteLine($"[PluginManager] Shutting down plugin {pluginId}...");
                await instance.PluginRuntime.ShutdownAsync(); // Даем плагину время остановить таймеры и закрыть файлы!
                
                // Освобождаем память!
                instance.LoadContext.Unload(); 
                Console.WriteLine($"[PluginManager] ALC for {pluginId} successfully unloaded.");
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
        // 1. Проверка API Scopes вызывающего (Zero Trust check)
        if (!_scopeManager.HasScope(sourcePluginId, $"api:plugin:{targetPluginId}"))
        {
            throw new UnauthorizedAccessException($"[Sandbox] Plugin {sourcePluginId} missing scope api:plugin:{targetPluginId} to perform Cross-Plugin RPC!");
        }

        if (!_loadedPlugins.TryGetValue(targetPluginId, out var targetPlugin))
            throw new InvalidOperationException($"Target plugin {targetPluginId} is offline or missing.");

        if (!targetPlugin.ExportedMethods.TryGetValue(method, out var methodInfo))
            throw new MissingMethodException($"Plugin {targetPluginId} strongly denies access or missing method: {method}");

        // 2. Сборка аргументов и Reflection-вызов
        var parameters = methodInfo.GetParameters();
        object?[] invokeArgs = new object?[parameters.Length];

        if (args != null && parameters.Length > 0)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var pName = parameters[i].Name;
                if (pName != null && args.TryGetValue(pName, out var val))
                {
                    // TODO: Реализовать глубокий JSON кастинг для сложной структуры аргументов
                    invokeArgs[i] = val; 
                }
            }
        }

        var result = methodInfo.Invoke(targetPlugin.PluginRuntime, invokeArgs);

        return Task.FromResult(result);
    }
}

// ----------------------------------------------------------------------
// LoadContext обеспечивает разделение зависимостей (Sandboxing) плагинов.
// Если Плагин А использует Newtonsoft 11, а Плагин Б - Newtonsoft 13, 
// они не войдут в Dependency Hell конфликт.
// ----------------------------------------------------------------------
class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // КРИТИЧЕСКИ ВАЖНО: Разделяемые (Shared) контракты должны грузиться только один раз в пространстве Ядра.
        // Иначе typeof(IPawsPlugin) в ядре != typeof(IPawsPlugin) в плагине.
        if (assemblyName.Name == "Paws.Abstractions")
        {
            return null; // Возврат null заставит CLR взять сборку из AssemblyLoadContext.Default (память Ядра)
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
