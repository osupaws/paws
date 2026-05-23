using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;
using Paws.Core.Plugins;

namespace Paws.Core.Storage;

/// <summary>
/// Core implementation of IStorageService.
/// Handles file blobs, plugin sandboxing, and security validation (The Fence).
/// </summary>
public class StorageService : IStorageService
{
    private readonly IDatabaseService _db;
    private readonly IScopeManager _scopeManager;
    private readonly IConfigService _config;
    private readonly IMonitoringService _monitoring;

    public StorageService(
        IDatabaseService db, 
        IScopeManager scopeManager, 
        IConfigService config,
        IMonitoringService monitoring)
    {
        _db = db;
        _scopeManager = scopeManager;
        _config = config;
        _monitoring = monitoring;
    }

    // --- Blobs ---

    public async Task<string> SaveBlobAsync(byte[] data, string contentType)
    {
        var hash = CalculateHash(data);
        var path = Path.Combine(_db.DataDirectory, hash);

        if (!File.Exists(path))
        {
            await File.WriteAllBytesAsync(path, data);
        }

        return hash;
    }

    public async Task<byte[]?> GetBlobAsync(string hash)
    {
        var path = Path.Combine(_db.DataDirectory, hash);
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path);
        }
        return null;
    }

    public Task<string?> GetBlobPathAsync(string hash)
    {
        var path = Path.Combine(_db.DataDirectory, hash);
        return Task.FromResult<string?>(File.Exists(path) ? path : null);
    }

    public void DeleteBlob(string hash)
    {
        var path = Path.Combine(_db.DataDirectory, hash);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string CalculateHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    // --- Sandbox & Temp ---

    public string GetPluginDataDirectory(string pluginId)
    {
        var basePath = Path.Combine(_db.PluginsDirectory, pluginId, "Data");
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
        return basePath;
    }

    public string GetPluginTempDirectory(string pluginId)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "Paws", "Plugins", pluginId);
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
        return basePath;
    }

    public async Task<byte[]> ReadPluginFileAsync(string pluginId, string relativePath)
    {
        var baseDir = GetPluginDataDirectory(pluginId);
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path outside plugin sandbox");
        
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Plugin file not found", fullPath);
        return await File.ReadAllBytesAsync(fullPath);
    }

    public async Task WritePluginFileAsync(string pluginId, string relativePath, byte[] data)
    {
        var baseDir = GetPluginDataDirectory(pluginId);
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path outside plugin sandbox");

        var dir = Path.GetDirectoryName(fullPath);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(fullPath, data);
    }

    // --- Atomic Bits (The Hands) ---

    public bool FileExists(string pluginId, string absolutePath)
    {
        if (!ValidateAccess(pluginId, absolutePath, false)) return false;
        return File.Exists(absolutePath);
    }

    public void DeleteFile(string pluginId, string absolutePath)
    {
        if (!ValidateAccess(pluginId, absolutePath, true))
            throw new UnauthorizedAccessException($"Plugin {pluginId} does not have write access to {absolutePath}");
        
        if (File.Exists(absolutePath)) File.Delete(absolutePath);
    }

    public bool DirectoryExists(string pluginId, string absolutePath)
    {
        if (!ValidateAccess(pluginId, absolutePath, false)) return false;
        return Directory.Exists(absolutePath);
    }

    public void DeleteDirectory(string pluginId, string absolutePath, bool recursive = false)
    {
        if (!ValidateAccess(pluginId, absolutePath, true))
            throw new UnauthorizedAccessException($"Plugin {pluginId} does not have write access to {absolutePath}");

        if (Directory.Exists(absolutePath)) Directory.Delete(absolutePath, recursive);
    }

    public IEnumerable<string> ListFiles(string pluginId, string absolutePath, string searchPattern = "*")
    {
        if (!ValidateAccess(pluginId, absolutePath, false))
            throw new UnauthorizedAccessException($"Plugin {pluginId} does not have read access to {absolutePath}");

        if (!Directory.Exists(absolutePath)) return Array.Empty<string>();
        return Directory.GetFiles(absolutePath, searchPattern).Select(Path.GetFileName)!;
    }

    public IEnumerable<string> ListDirectories(string pluginId, string absolutePath, string searchPattern = "*")
    {
        if (!ValidateAccess(pluginId, absolutePath, false))
            throw new UnauthorizedAccessException($"Plugin {pluginId} does not have read access to {absolutePath}");

        if (!Directory.Exists(absolutePath)) return Array.Empty<string>();
        return Directory.GetDirectories(absolutePath, searchPattern).Select(Path.GetFileName)!;
    }

    // --- The Fence (Validation) ---

    public bool ValidateAccess(string pluginId, string absolutePath, bool isWriteAccess = false)
    {
        var normalizedPath = Path.GetFullPath(absolutePath).TrimEnd('\\', '/');

        // 0. Host (Sidecar core) always has access
        if (pluginId == "host") return true;

        // 1. Always allow access to plugin's own Data and Temp
        var dataDir = GetPluginDataDirectory(pluginId).TrimEnd('\\', '/');
        var tempDir = GetPluginTempDirectory(pluginId).TrimEnd('\\', '/');
        
        if (normalizedPath.StartsWith(dataDir, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. Check Static Scopes (Stable / Lazer)
        var isOsuPath = false;
        var isStable = false;
        var isLazer = false;

        var stablePath = _config.Config.StablePath?.TrimEnd('\\', '/');
        var lazerPath = _config.Config.LazerPath?.TrimEnd('\\', '/');

        if (!string.IsNullOrEmpty(stablePath) && normalizedPath.StartsWith(stablePath, StringComparison.OrdinalIgnoreCase))
        {
            isOsuPath = true;
            isStable = true;
        }
        else if (!string.IsNullOrEmpty(lazerPath) && normalizedPath.StartsWith(lazerPath, StringComparison.OrdinalIgnoreCase))
        {
            isOsuPath = true;
            isLazer = true;
        }

        if (isOsuPath)
        {
            // Check Scope
            var requiredScope = isWriteAccess ? "filesystem-osu:write" : "filesystem-osu:read";
            // Allow if has either granular scope or the legacy "filesystem-osu"
            if (!_scopeManager.HasScope(pluginId, requiredScope) && !_scopeManager.HasScope(pluginId, "filesystem-osu"))
                return false;

            // 3. Safety Check: Block Write if Game is Running
            if (isWriteAccess)
            {
                var state = _monitoring.CurrentState;
                if (state.IsOsuRunning)
                {
                    // If target is Stable and Stable is running
                    if (isStable && state.ActiveClient == GameClientType.Stable)
                        throw new InvalidOperationException("Cannot write to osu!stable folder while the game is running.");
                    
                    // If target is Lazer and Lazer is running
                    if (isLazer && state.ActiveClient == GameClientType.Lazer)
                        throw new InvalidOperationException("Cannot write to osu!lazer folder while the game is running.");
                }
            }

            return true;
        }

        // 4. External Access (fs:ext)
        if (_scopeManager.HasScope(pluginId, isWriteAccess ? "filesystem-ext:write" : "filesystem-ext:read") || 
            _scopeManager.HasScope(pluginId, "filesystem-ext"))
        {
            return true;
        }

        // 5. Runtime Granted Folders
        var runtimeFolders = _scopeManager.GetRuntimeAllowedFolders(pluginId);
        if (runtimeFolders.Any(f => normalizedPath.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
