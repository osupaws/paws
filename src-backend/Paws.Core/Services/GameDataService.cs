using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Models.Game;
using Paws.Abstractions.Services;
using Paws.Drivers.Lazer;
using Paws.Drivers.Stable;

namespace Paws.Core.Services;

/// <summary>
/// Gateway service that directs database requests to either Lazer or Stable drivers.
/// Handles asset path resolution and atomic mutations on game records.
/// </summary>
public class GameDataService : IGameDataService
{
    private readonly IConfigService _configService;

    public GameDataService(IConfigService configService)
    {
        _configService = configService;
    }

    private LazerDbService Lazer => new LazerDbService(_configService.Config.LazerPath);
    private StableDbService Stable => new StableDbService(_configService.Config.StablePath);

    public bool IsLazerDatabaseAvailable => Lazer.IsAvailable;
                                           
    public bool IsStableDatabaseAvailable => Stable.IsAvailable;

    public GameClientType GetActiveClient() => _configService.Config.IsLegacyMode ? GameClientType.Stable : GameClientType.Lazer;

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
        Console.WriteLine("[GameDataService] Initializing Game Data...");
        
        if (IsLazerDatabaseAvailable)
            Console.WriteLine($"[GameDataService] Lazer found at: {_configService.Config.LazerPath}");
            
        if (IsStableDatabaseAvailable)
            Console.WriteLine($"[GameDataService] Stable found at: {_configService.Config.StablePath}");
    }

    public async Task<GameBeatmap?> GetBeatmapByHashAsync(string md5Hash)
    {
        if (_configService.Config.IsLegacyMode)
        {
            return await Task.Run(() => IsStableDatabaseAvailable ? Stable.GetBeatmapByHash(md5Hash) : null);
        }
        else
        {
            return await Task.Run(() => IsLazerDatabaseAvailable ? Lazer.GetBeatmapByHash(md5Hash) : null);
        }
    }

    public Task<IEnumerable<GameBeatmapSet>> GetAllBeatmapSetsAsync()
    {
        var allSets = new List<GameBeatmapSet>();
        
        if (_configService.Config.IsLegacyMode)
        {
            if (IsStableDatabaseAvailable) allSets.AddRange(Stable.GetAllBeatmapSets());
        }
        else
        {
            if (IsLazerDatabaseAvailable) allSets.AddRange(Lazer.GetAllBeatmapSets());
        }
        
        return Task.FromResult<IEnumerable<GameBeatmapSet>>(allSets);
    }

    public Task<IEnumerable<GameBeatmap>> SearchBeatmapsAsync(string query)
    {
        var allSets = GetAllBeatmapSetsAsync().GetAwaiter().GetResult();
        var results = allSets.SelectMany(s => s.Beatmaps)
            .Where(b => 
                b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                b.Artist.Contains(query, StringComparison.OrdinalIgnoreCase));
            
        return Task.FromResult(results);
    }

    public Task<IEnumerable<GameCollection>> GetAllCollectionsAsync()
    {
        var list = new List<GameCollection>();
        if (_configService.Config.IsLegacyMode)
        {
            if (IsStableDatabaseAvailable) list.AddRange(Stable.GetAllCollections());
        }
        else
        {
            if (IsLazerDatabaseAvailable) list.AddRange(Lazer.GetAllCollections());
        }
        return Task.FromResult<IEnumerable<GameCollection>>(list);
    }

    public Task<IEnumerable<GameScore>> GetScoresByBeatmapHashAsync(string md5Hash)
    {
        var list = new List<GameScore>();
        if (_configService.Config.IsLegacyMode)
        {
            if (IsStableDatabaseAvailable) list.AddRange(Stable.GetScoresByBeatmapHash(md5Hash));
        }
        else
        {
            if (IsLazerDatabaseAvailable) list.AddRange(Lazer.GetScoresByBeatmapHash(md5Hash));
        }
        return Task.FromResult<IEnumerable<GameScore>>(list);
    }

    public Task<IEnumerable<GameSkin>> GetAllSkinsAsync()
    {
        var list = new List<GameSkin>();
        if (!_configService.Config.IsLegacyMode)
        {
            if (IsLazerDatabaseAvailable) list.AddRange(Lazer.GetAllSkins());
        }
        // TODO: Implement Stable skin discovery if in Legacy mode
        return Task.FromResult<IEnumerable<GameSkin>>(list);
    }

    public Task<string?> GetFilePathAsync(string? hash, string? folderName, string? filename)
    {
        // 1. Try Lazer (Content-addressable file store)
        if (!string.IsNullOrEmpty(hash) && IsLazerDatabaseAvailable && hash.Length >= 2)
        {
            var lazerBasePath = _configService.Config.LazerPath;
            var directory1 = hash.Substring(0, 1);
            var directory2 = hash.Substring(0, 2);
            var lazerFilePath = Path.Combine(lazerBasePath, "files", directory1, directory2, hash);
            
            if (File.Exists(lazerFilePath)) 
                return Task.FromResult<string?>(lazerFilePath);
        }

        // 2. Try Stable (Classic Songs/Folder/File structure)
        if (!string.IsNullOrEmpty(folderName) && !string.IsNullOrEmpty(filename) && IsStableDatabaseAvailable)
        {
            var stableBasePath = _configService.Config.StablePath;
            var stableFilePath = Path.Combine(stableBasePath, "Songs", folderName, filename);
            
            if (File.Exists(stableFilePath)) 
                return Task.FromResult<string?>(stableFilePath);
        }

        return Task.FromResult<string?>(null);
    }

    // --- Атомарные мутаторы (The Hands) ---

    public async Task<bool> DeleteRecordAsync(string pluginId, GameClientType client, string type, string id)
    {
        // In Paws v3 this was part of IStorageService.ValidateAccess, but we moved it here in Paws-Next.
        Console.WriteLine($"[GameDataService] Plugin {pluginId} requested DELETE on {client}:{type} ID={id}");

        if (client == GameClientType.Lazer)
        {
            return await Task.FromResult(Lazer.DeleteRecord(type, id)); 
        }
        else if (client == GameClientType.Stable)
        {
            return await Task.FromResult(Stable.DeleteRecord(type, id));
        }

        return false;
    }

    public async Task<bool> UpdateRecordAsync(string pluginId, GameClientType client, string type, string id, object data)
    {
        Console.WriteLine($"[GameDataService] Plugin {pluginId} requested UPDATE on {client}:{type} ID={id}");
        
        if (client == GameClientType.Lazer)
        {
            return await Task.FromResult(Lazer.UpdateRecord(type, id, data));
        }

        return await Task.FromResult(false);
    }
}
