using System.Collections.Generic;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for managing plugin permissions (Scopes).
/// </summary>
public interface IScopeManager
{
    // Register static scopes from the plugin manifest
    void RegisterPluginScopes(string pluginId, IEnumerable<string> scopes);

    // Check if a plugin has a specific scope (e.g. "filesystem-osu:write")
    bool HasScope(string pluginId, string scopeName);
    
    // Grants temporary runtime access to a specific folder
    void GrantRuntimeScope(string pluginId, string folderPath);
    
    // List all folders granted via runtime scopes
    IEnumerable<string> GetRuntimeAllowedFolders(string pluginId);

    // Clears all temporary runtime scopes
    void RevokeAllRuntimeScopes(string pluginId);
}
