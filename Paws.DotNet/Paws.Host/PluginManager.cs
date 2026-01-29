using Paws.Core.Abstractions;
using Paws.Host.Data.Schemas;
using System.Reflection;
using System.Runtime.Loader;

namespace Paws.Host;

/// <summary>
/// Manages the discovery, loading, and access to all Paws plugins using the Realm database.
/// </summary>
public class PluginManager
{
    // Key: Plugin DB ID (string), Value: Plugin Instance
    private readonly Dictionary<string, IFunctionalExplicitPlugin> _loadedPlugins = new();
    private readonly IHostServices _hostServices;
    private readonly ILogger<PluginManager> _logger;
    private readonly PawsDbService _dbService;
    private readonly FileStorageService _storage;

    // We store contexts to ensure they don't get collected while the plugin is running
    // Key: Plugin DB ID (string)
    private readonly Dictionary<string, AssemblyLoadContext> _pluginContexts = new();

    public PluginManager(IHostServices hostServices, ILogger<PluginManager> logger, PawsDbService dbService, FileStorageService storage)
    {
        _hostServices = hostServices;
        _logger = logger;
        _dbService = dbService;
        _storage = storage;
    }

    /// <summary>
    /// Loads all active plugins from the database. Skips already loaded plugins.
    /// </summary>
    public void DiscoverAndLoadPlugins()
    {
        _logger.LogInformation("Starting plugin discovery from database...");

        var config = _dbService.GetRealmConfiguration();
        using var realm = Realms.Realm.GetInstance(config);

        var allPlugins = realm.All<Plugin>().ToList();

        foreach (var plugin in allPlugins)
        {
            if (!plugin.IsActive)
            {
                // Optionally unload if it was previously loaded but now disabled
                 if (_loadedPlugins.ContainsKey(plugin.Id))
                 {
                     _logger.LogInformation("Plugin '{Name}' ({Id}) is now disabled. Unloading...", plugin.Name, plugin.Id);
                     UnloadPlugin(plugin.Id);
                 }
                continue;
            }

            // IDEMPOTENCY CHECK: If already loaded, skip.
            if (_loadedPlugins.ContainsKey(plugin.Id))
            {
                continue;
            }

            LoadSinglePlugin(plugin);
        }

        _logger.LogInformation("Plugin loading finished. {Count} plugins loaded.", _loadedPlugins.Count);
    }

    /// <summary>
    /// Unloads and then reloads a specific plugin by ID.
    /// </summary>
    public void ReloadPlugin(string pluginId)
    {
        _logger.LogInformation("Reloading plugin: {Id}", pluginId);

        // 1. Unload existing
        UnloadPlugin(pluginId);

        // 2. Load from DB
        var config = _dbService.GetRealmConfiguration();
        using var realm = Realms.Realm.GetInstance(config);

        var plugin = realm.Find<Plugin>(pluginId);
        if (plugin != null && plugin.IsActive)
        {
            LoadSinglePlugin(plugin);
        }
        else
        {
            _logger.LogWarning("Plugin {Id} not found or inactive during reload.", pluginId);
        }
    }

    private void UnloadPlugin(string pluginId)
    {
        if (_loadedPlugins.Remove(pluginId, out var instance))
        {
             // If the plugin supports explicit shutdown/dispose, call it here.
             // if (instance is IDisposable disposable) disposable.Dispose();
             _logger.LogInformation("Unloaded plugin instance: {Name}", instance.Name);
        }

        if (_pluginContexts.Remove(pluginId, out var context))
        {
            context.Unload();
            _logger.LogInformation("Unloaded AssemblyLoadContext for: {Id}", pluginId);
        }
    }

    private void LoadSinglePlugin(Plugin plugin)
    {
        try
        {
            if (string.IsNullOrEmpty(plugin.EntryPoint))
            {
                _logger.LogError("Plugin '{Name}' has no EntryPoint defined.", plugin.Name);
                return;
            }

            // Create a custom load context for isolation and DB loading
            var context = new DbPluginLoadContext(_dbService, _storage, _logger, plugin.Id);

            // Derive assembly name from EntryPoint (e.g. "MyPlugin.dll" -> "MyPlugin")
            var assemblyNameStr = Path.GetFileNameWithoutExtension(plugin.EntryPoint);
            var assemblyName = new AssemblyName(assemblyNameStr);

            var assembly = context.LoadFromAssemblyName(assemblyName);

            if (assembly == null)
            {
                 _logger.LogError("Failed to load assembly '{EntryPoint}' for plugin '{Name}'.", plugin.EntryPoint, plugin.Name);
                 return;
            }

            var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                 _logger.LogError("No type implementing IPlugin found in '{EntryPoint}'.", plugin.EntryPoint);
                 return;
            }

            // Create an instance of the plugin and initialize it.
            if (Activator.CreateInstance(pluginType) is IFunctionalExplicitPlugin pluginInstance)
            {
                pluginInstance.Initialize(_hostServices);

                // Store in Dictionary keyed by DB ID string
                _loadedPlugins[plugin.Id] = pluginInstance;
                _pluginContexts[plugin.Id] = context;

                _logger.LogInformation("Successfully loaded plugin: {Name} (v{Version})", pluginInstance.Name, pluginInstance.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while loading plugin '{Name}'.", plugin.Name);
        }
    }

    /// <summary>Gets a read-only list of plugins that are loaded and running.</summary>
    public IEnumerable<IFunctionalExplicitPlugin> GetLoadedPlugins() => _loadedPlugins.Values;

    /// <summary>Gets a list of all installed plugins (active and inactive).</summary>
    public IEnumerable<PluginManifest> GetAllPlugins()
    {
        var config = _dbService.GetRealmConfiguration();
        using var realm = Realms.Realm.GetInstance(config);

        return realm.All<Plugin>().ToList().Select(p => {
             return new PluginManifest(
                p.Id,
                p.Name,
                p.Version,
                p.EntryPoint,
                p.Author,
                p.Description,
                string.IsNullOrEmpty(p.UiEntry) ? null : new PluginUiManifest(p.UiEntry),
                p.IconData, // Pass raw SVG (or null) directly
                p.Permissions.ToList(),
                p.Provides.ToList(),
                p.Consumes.ToList(),
                p.IsActive
            );
        }).ToList();
    }

    /// <summary>Sets the active state of a plugin and loads/unloads it accordingly.</summary>
    public void SetPluginActive(string pluginId, bool isActive)
    {
        var config = _dbService.GetRealmConfiguration();
        using var realm = Realms.Realm.GetInstance(config);

        var plugin = realm.Find<Plugin>(pluginId);
        if (plugin == null) return;

        realm.Write(() =>
        {
            plugin.IsActive = isActive;
        });

        if (isActive)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                LoadSinglePlugin(plugin);
            }
        }
        else
        {
            UnloadPlugin(pluginId);
        }
    }

    /// <summary>Retrieves a specific loaded plugin by its unique ID.</summary>
    public IFunctionalExplicitPlugin? GetPluginById(Guid pluginGuid)
    {
        // This is inefficient (O(N)) but usually N is small.
        // If needed we can maintain a secondary Guid->Instance map.
        return _loadedPlugins.Values.FirstOrDefault(p => p.Id == pluginGuid);
    }
}

// --- Manifest Records (Updated for API) ---
public record PluginManifest(
    string Id,
    string Name,
    string Version,
    string EntryPoint,
    string? Author,
    string? Description,
    PluginUiManifest? Ui,
    string? Icon, // Added Icon URL
    List<string>? Permissions = null,
    List<string>? Provides = null,
    List<string>? Consumes = null,
    bool IsActive = true
);

public record PluginUiManifest(
    string Entry
);
