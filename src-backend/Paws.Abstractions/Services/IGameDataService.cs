using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Models.Game;

namespace Paws.Abstractions.Services;

public interface IGameDataService
{
    // Статус
    bool IsLazerDatabaseAvailable { get; }
    bool IsStableDatabaseAvailable { get; }

    // Основные методы поиска
    Task<GameBeatmap?> GetBeatmapByHashAsync(string md5Hash);
    Task<IEnumerable<GameBeatmapSet>> GetAllBeatmapSetsAsync();
    
    // Специализированные методы
    Task<IEnumerable<GameBeatmap>> SearchBeatmapsAsync(string query);
    
    // Универсальный метод получения физического пути к любому файлу игры
    // Для Lazer укажите hash (из GameFileUsage). 
    // Для Stable укажите folderName (папка сета) и filename.
    Task<string?> GetFilePathAsync(string? hash, string? folderName, string? filename);

    // Коллекции
    Task<IEnumerable<GameCollection>> GetAllCollectionsAsync();
    
    // Рекорды
    Task<IEnumerable<GameScore>> GetScoresByBeatmapHashAsync(string md5Hash);
    
    // Скины
    Task<IEnumerable<GameSkin>> GetAllSkinsAsync();
}
