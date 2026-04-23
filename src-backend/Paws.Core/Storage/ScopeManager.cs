using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Services;

namespace Paws.Core.Storage;

public class ScopeManager : IScopeManager
{
    // Runtime-скоупы. Позже будем сохранять их в Host БД, чтобы права не сбрасывались после перезапуска.
    private readonly ConcurrentDictionary<string, HashSet<string>> _runtimeFolders = new();

    public bool HasScope(string pluginId, string scopeName)
    {
        // Ядру (core) разрешено всё
        if (string.Equals(pluginId, "core", StringComparison.OrdinalIgnoreCase)) return true;

        // В будущем здесь мы будем парсить _pluginManager.GetManifest(pluginId).Scopes
        return false;
    }

    public void GrantRuntimeScope(string pluginId, string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        
        var normalizedPath = folderPath.TrimEnd('\\', '/');
        
        var folders = _runtimeFolders.GetOrAdd(pluginId, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        folders.Add(normalizedPath);
    }

    public IEnumerable<string> GetRuntimeAllowedFolders(string pluginId)
    {
        return _runtimeFolders.TryGetValue(pluginId, out var folders) ? folders : Array.Empty<string>();
    }
}
