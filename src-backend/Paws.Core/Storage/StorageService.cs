using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Paws.Abstractions.Services;
using Paws.Core.Data;
using Realms;

namespace Paws.Core.Storage;

public class StorageService : IStorageService
{
    private readonly IDatabaseService _db;
    private readonly IScopeManager _scopeManager;
    private readonly IConfigService _config;

    public StorageService(IDatabaseService db, IScopeManager scopeManager, IConfigService config)
    {
        _db = db;
        _scopeManager = scopeManager;
        _config = config;
    }

    private Realm _realm => _db.GetRealm();

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

    // --- Sandbox Implementation ---

    public string GetPluginDataDirectory(string pluginId)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Data", "Plugins", pluginId, "Data");
        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }
        return basePath;
    }

    private string GetValidatedPluginPath(string pluginId, string relativePath)
    {
        var baseDir = GetPluginDataDirectory(pluginId);
        var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));

        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Plugin {pluginId} is not allowed to access path outside its sandbox: {relativePath}");
        }

        return fullPath;
    }

    public async Task<byte[]> ReadPluginFileAsync(string pluginId, string relativePath)
    {
        var path = GetValidatedPluginPath(pluginId, relativePath);
        if (!File.Exists(path)) throw new FileNotFoundException("Plugin file not found", path);
        return await File.ReadAllBytesAsync(path);
    }

    public async Task WritePluginFileAsync(string pluginId, string relativePath, byte[] data)
    {
        var path = GetValidatedPluginPath(pluginId, relativePath);
        
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(path, data);
    }

    public bool ValidatePathAccess(string pluginId, string absolutePath)
    {
        var normalizedPath = Path.GetFullPath(absolutePath).TrimEnd('\\', '/');

        // 1. fs:self (своя папка)
        var baseDir = GetPluginDataDirectory(pluginId).TrimEnd('\\', '/');
        if (normalizedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Статические права (из plugin.json)
        if (_scopeManager.HasScope(pluginId, "fs:stable:read") || _scopeManager.HasScope(pluginId, "fs:stable:write"))
        {
            var stablePath = _config.Config.StablePath?.TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(stablePath) && normalizedPath.StartsWith(stablePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (_scopeManager.HasScope(pluginId, "fs:lazer:read") || _scopeManager.HasScope(pluginId, "fs:lazer:write"))
        {
            var lazerPath = _config.Config.LazerPath?.TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(lazerPath) && normalizedPath.StartsWith(lazerPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 3. Динамические права (10 ГБ ассетов пользователя)
        var runtimeFolders = _scopeManager.GetRuntimeAllowedFolders(pluginId);
        if (runtimeFolders.Any(f => normalizedPath.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
