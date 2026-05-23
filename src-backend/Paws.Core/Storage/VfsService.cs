using System;
using System.IO;
using Paws.Abstractions.Services;

namespace Paws.Core.Storage;

/// <summary>
/// Implementation of IVfsService for resolving virtual paths (game://, paws://).
/// Handles abstraction of Lazer's content-addressable storage.
/// </summary>
public class VfsService : IVfsService
{
    private readonly IConfigService _config;
    private readonly IStorageService _storage;

    public VfsService(IConfigService config, IStorageService storage)
    {
        _config = config;
        _storage = storage;
    }

    public string ResolvePath(string pluginId, string vPath)
    {
        if (string.IsNullOrEmpty(vPath)) return string.Empty;

        string host;
        string path;

        if (vPath.StartsWith("game://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(vPath);
            host = uri.Host.ToLowerInvariant();
            path = uri.AbsolutePath.TrimStart('/');
        }
        else if (vPath.StartsWith("paws://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(vPath);
            host = uri.Host.ToLowerInvariant();
            path = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
             throw new ArgumentException("Invalid VFS protocol. Expected game:// or paws://", nameof(vPath));
        }

        return host switch
        {
            "stable" => Path.Combine(_config.Config.StablePath ?? string.Empty, path),
            "lazer" => ResolveLazerPath(path),
            "plugin" => Path.Combine(_storage.GetPluginDataDirectory(pluginId), path),
            "temp" => Path.Combine(_storage.GetPluginTempDirectory(pluginId), path),
            "active" => ResolveActivePath(pluginId, path),
            _ => throw new NotSupportedException($"VFS Provider '{host}' not supported.")
        };
    }

    private string ResolveLazerPath(string path)
    {
        var lazerBase = _config.Config.LazerPath ?? string.Empty;
        
        // --- The "Dirty Work" (Lazer Hash Abstraction) ---
        // If path looks like a hash (e.g. from GameDataService), we automatically find it in files/
        if (path.Length >= 32 && !path.Contains("/") && !path.Contains("\\"))
        {
            var hash = path.ToLowerInvariant();
            return Path.Combine(lazerBase, "files", hash.Substring(0, 1), hash.Substring(0, 2), hash);
        }

        return Path.Combine(lazerBase, path);
    }

    private string ResolveActivePath(string pluginId, string path)
    {
        var isLegacy = _config.Config.IsLegacyMode;
        var targetHost = isLegacy ? "stable" : "lazer";
        return ResolvePath(pluginId, $"game://{targetHost}/{path}");
    }

    public bool ValidateAccess(string pluginId, string vPath)
    {
        try
        {
            var absolutePath = ResolvePath(pluginId, vPath);
            return _storage.ValidateAccess(pluginId, absolutePath);
        }
        catch
        {
            return false;
        }
    }
}
