using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Plugins;
using Paws.Abstractions.Services;

namespace Paws.Core.Plugins;

/// <summary>
/// Implementation of ISandboxedStorage that wraps IStorageService for a specific plugin.
/// Ensures all file operations are relative to the plugin's sandbox or validated against scopes.
/// </summary>
public class SandboxedStorage : ISandboxedStorage
{
    private readonly IStorageService _storage;
    private readonly string _pluginId;

    public SandboxedStorage(IStorageService storage, string pluginId)
    {
        _storage = storage;
        _pluginId = pluginId;
    }

    public async Task<byte[]> ReadFileAsync(string relativePath)
    {
        return await _storage.ReadPluginFileAsync(_pluginId, relativePath);
    }

    public async Task WriteFileAsync(string relativePath, byte[] data)
    {
        await _storage.WritePluginFileAsync(_pluginId, relativePath, data);
    }

    public string GetDataDirectory() => _storage.GetPluginDataDirectory(_pluginId);
    public string GetTempDirectory() => _storage.GetPluginTempDirectory(_pluginId);

    public bool FileExists(string path) => _storage.FileExists(_pluginId, path);
    public void DeleteFile(string path) => _storage.DeleteFile(_pluginId, path);
    
    public bool DirectoryExists(string path) => _storage.DirectoryExists(_pluginId, path);
    public void DeleteDirectory(string path, bool recursive = false) 
        => _storage.DeleteDirectory(_pluginId, path, recursive);

    public IEnumerable<string> ListFiles(string path, string searchPattern = "*")
        => _storage.ListFiles(_pluginId, path, searchPattern);

    public IEnumerable<string> ListDirectories(string path, string searchPattern = "*")
        => _storage.ListDirectories(_pluginId, path, searchPattern);

    public async Task<byte[]> ReadAbsolutePathAsync(string absolutePath)
    {
        if (!_storage.ValidateAccess(_pluginId, absolutePath, false))
        {
            throw new UnauthorizedAccessException($"Plugin {_pluginId} does not have scope to read {absolutePath}");
        }

        if (!System.IO.File.Exists(absolutePath))
            throw new System.IO.FileNotFoundException("File not found", absolutePath);

        return await System.IO.File.ReadAllBytesAsync(absolutePath);
    }
}

/// <summary>
/// Implementation of IHostApi that provides plugins with access to core services.
/// This acts as a security boundary between the plugin and the kernel.
/// </summary>
public class HostApi : IHostApi
{
    private readonly string _pluginId;
    private readonly IPluginManager _pluginManager;

    public HostApi(
        string pluginId, 
        IStorageService globalStorage, 
        IGameDataService gameData,
        IMonitoringService monitor,
        IVfsService vfs,
        IPluginManager pluginManager)
    {
        _pluginId = pluginId;
        Storage = new SandboxedStorage(globalStorage, pluginId);
        GameData = gameData;
        Monitor = monitor;
        Vfs = vfs;
        _pluginManager = pluginManager;
    }

    public ISandboxedStorage Storage { get; }
    public IGameDataService GameData { get; }
    public IMonitoringService Monitor { get; }
    public IVfsService Vfs { get; }

    public Task<object?> InvokePluginAsync(string targetPluginId, string method, Dictionary<string, object>? args = null)
    {
        return _pluginManager.InvokePluginMethodAsync(_pluginId, targetPluginId, method, args);
    }
}
