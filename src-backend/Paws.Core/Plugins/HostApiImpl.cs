using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Plugins;
using Paws.Abstractions.Services;

namespace Paws.Core.Plugins;

/// <summary>
/// Обертка вокруг глобального StorageService.
/// Плагины получают только этот класс, куда Ядро 'вживляет' их ID.
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

    public async Task<byte[]> ReadAbsolutePathAsync(string absolutePath)
    {
        if (!_storage.ValidatePathAccess(_pluginId, absolutePath))
        {
            throw new UnauthorizedAccessException($"Plugin {_pluginId} does not have scope to read {absolutePath}");
        }

        if (!System.IO.File.Exists(absolutePath))
            throw new System.IO.FileNotFoundException("File not found via absolute path", absolutePath);

        return await System.IO.File.ReadAllBytesAsync(absolutePath);
    }
}

/// <summary>
/// Безопасный фасад Ядра для конкретного плагина.
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
        IPluginManager pluginManager)
    {
        _pluginId = pluginId;
        Storage = new SandboxedStorage(globalStorage, pluginId);
        GameData = gameData;
        Monitor = monitor;
        _pluginManager = pluginManager;
    }

    public ISandboxedStorage Storage { get; }
    public IGameDataService GameData { get; }
    public IMonitoringService Monitor { get; }

    public Task<object?> InvokePluginAsync(string targetPluginId, string method, Dictionary<string, object>? args = null)
    {
        // Проверяем права плагина A вызывать плагин B (api:plugin:target_id)
        // Внутри _pluginManager мы делегируем проверку Scopes.
        return _pluginManager.InvokePluginMethodAsync(_pluginId, targetPluginId, method, args);
    }
}
