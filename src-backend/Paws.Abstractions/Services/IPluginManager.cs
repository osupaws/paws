using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for loading, managing, and facilitating communication between plugins.
/// </summary>
public interface IPluginManager
{
    // Scans the Plugins directory and loads manifests and assemblies
    Task LoadPluginsAsync();
    
    // Returns a list of all currently loaded plugins
    IEnumerable<PluginManifest> GetLoadedPlugins();
    
    // Returns the manifest for a specific plugin ID
    PluginManifest? GetManifest(string pluginId);

    // Cross-Plugin API: Invokes a [PublicEntryPoint] method on a loaded plugin
    Task<object?> InvokePluginMethodAsync(string sourcePluginId, string targetPluginId, string method, Dictionary<string, object>? args);

    // Loads a plugin candidate from an arbitrary path (Developer mode)
    Task LoadDevPluginAsync(string absolutePathToFolder);

    // Hot-unloads a plugin from memory
    Task UnloadPluginAsync(string pluginId);
}
