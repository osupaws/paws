using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Models.Game;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for accessing game databases and beatmap metadata.
/// Supports both osu!stable and osu!lazer.
/// </summary>
public interface IGameDataService
{
    // Status
    bool IsLazerDatabaseAvailable { get; }
    bool IsStableDatabaseAvailable { get; }
    GameClientType GetActiveClient();

    // Core Search Methods
    Task<GameBeatmap?> GetBeatmapByHashAsync(string md5Hash);
    Task<IEnumerable<GameBeatmapSet>> GetAllBeatmapSetsAsync();
    
    // Specialized Search
    Task<IEnumerable<GameBeatmap>> SearchBeatmapsAsync(string query);
    
    /// <summary>
    /// Returns the absolute physical path to a game asset.
    /// For Lazer: provide the file hash (GameFileUsage.Hash).
    /// For Stable: provide folderName (set folder) and filename.
    /// </summary>
    Task<string?> GetFilePathAsync(string? hash, string? folderName, string? filename);

    // Collections
    Task<IEnumerable<GameCollection>> GetAllCollectionsAsync();
    
    // Scores
    Task<IEnumerable<GameScore>> GetScoresByBeatmapHashAsync(string md5Hash);
    
    // Skins
    Task<IEnumerable<GameSkin>> GetAllSkinsAsync();

    // Atomic Mutators (The Hands)
    Task<bool> DeleteRecordAsync(string pluginId, GameClientType client, string type, string id);
    Task<bool> UpdateRecordAsync(string pluginId, GameClientType client, string type, string id, object data);
}
