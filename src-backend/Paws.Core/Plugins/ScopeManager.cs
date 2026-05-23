using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Services;

namespace Paws.Core.Plugins;

/// <summary>
/// Core implementation of IScopeManager.
/// Tracks static and runtime permissions granted to plugins.
/// </summary>
public class ScopeManager : IScopeManager
{
    // Static scopes from plugin.json (PluginID -> List of Scopes)
    private readonly ConcurrentDictionary<string, HashSet<string>> _staticScopes = new();
    
    // Dynamic runtime allowed folders (PluginID -> List of Paths)
    private readonly ConcurrentDictionary<string, HashSet<string>> _runtimeFolders = new();

    public void RegisterPluginScopes(string pluginId, IEnumerable<string> scopes)
    {
        var set = _staticScopes.GetOrAdd(pluginId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        foreach (var scope in scopes)
        {
            set.Add(scope);
        }
    }

    public bool HasScope(string pluginId, string scope)
    {
        if (pluginId == "host") return true; // Host has all permissions
        
        if (_staticScopes.TryGetValue(pluginId, out var scopes))
        {
            return scopes.Contains(scope);
        }
        return false;
    }

    public void GrantRuntimeScope(string pluginId, string folderPath)
    {
        var set = _runtimeFolders.GetOrAdd(pluginId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        set.Add(folderPath);
    }

    public IEnumerable<string> GetRuntimeAllowedFolders(string pluginId)
    {
        if (_runtimeFolders.TryGetValue(pluginId, out var folders))
        {
            return folders;
        }
        return Enumerable.Empty<string>();
    }

    public void RevokeAllRuntimeScopes(string pluginId)
    {
        _runtimeFolders.TryRemove(pluginId, out _);
    }
}
