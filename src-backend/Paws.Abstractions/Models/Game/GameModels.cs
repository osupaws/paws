using System;
using System.Collections.Generic;

namespace Paws.Abstractions.Models.Game;

public enum GameMode
{
    Osu = 0,
    Taiko = 1,
    Catch = 2,
    Mania = 3
}

public class GameBeatmap
{
    // Идентификация
    public string Hash { get; set; } = string.Empty;
    public long OnlineId { get; set; }
    public string DifficultyName { get; set; } = string.Empty;
    public string Md5Hash { get; set; } = string.Empty;

    // Метаданные
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;

    // Сложность (полное покрытие)
    public float StarRating { get; set; }
    public float ApproachRate { get; set; }
    public float CircleSize { get; set; }
    public float OverallDifficulty { get; set; }
    public float DrainRate { get; set; }
    public double Bpm { get; set; }

    // Статистика объектов
    public int CircleCount { get; set; }
    public int SliderCount { get; set; }
    public int SpinnerCount { get; set; }

    public GameMode Mode { get; set; }
    public long BeatmapSetId { get; set; }

    // Файлы
    public string AudioFile { get; set; } = string.Empty;
    public string BackgroundFile { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;

    public int RankedStatus { get; set; } // 1=Ranked, 2=Approved...
    public DateTimeOffset? LastPlayed { get; set; }
}

public class GameBeatmapSet
{
    public long OnlineId { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public List<GameBeatmap> Beatmaps { get; set; } = new();
}

public class GameScore
{
    public Guid Id { get; set; }
    public long OnlineId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public long TotalScore { get; set; }
    public float Accuracy { get; set; }
    public int MaxCombo { get; set; }
    public string Rank { get; set; } = string.Empty; // S, SS, A
    public DateTimeOffset Date { get; set; }
    public double? PP { get; set; }

    // Статистика (300, 100, 50, Miss)
    public Dictionary<string, int> Statistics { get; set; } = new();
    public List<string> Mods { get; set; } = new();

    public string BeatmapHash { get; set; } = string.Empty;
}

public class GameSkin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<GameFileUsage> Files { get; set; } = new();
}

public class GameFileUsage
{
    public string Filename { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty; // SHA-2 хеш для поиска в files/
}

public class GameCollection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> BeatmapHashes { get; set; } = new();
}
