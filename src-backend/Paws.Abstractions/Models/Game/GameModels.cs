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

/// <summary>
/// Domain model for a single beatmap (difficulty).
/// </summary>
public class GameBeatmap
{
    // Identification
    public string Hash { get; set; } = string.Empty;
    public long OnlineId { get; set; }
    public string DifficultyName { get; set; } = string.Empty;
    public string Md5Hash { get; set; } = string.Empty;

    // Metadata
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;

    // Difficulty details
    public float StarRating { get; set; }
    public float ApproachRate { get; set; }
    public float CircleSize { get; set; }
    public float OverallDifficulty { get; set; }
    public float DrainRate { get; set; }
    public double Bpm { get; set; }

    // Object counts
    public int CircleCount { get; set; }
    public int SliderCount { get; set; }
    public int SpinnerCount { get; set; }

    public GameMode Mode { get; set; }
    public long BeatmapSetId { get; set; }

    // File references
    public string AudioFile { get; set; } = string.Empty;
    public string BackgroundFile { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;

    public int RankedStatus { get; set; } // 1=Ranked, 2=Approved...
    public DateTimeOffset? LastPlayed { get; set; }
}

/// <summary>
/// Domain model for a beatmap set (collection of difficulties).
/// </summary>
public class GameBeatmapSet
{
    public long OnlineId { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public List<GameBeatmap> Beatmaps { get; set; } = new();
    public List<GameFileUsage> Files { get; set; } = new();
}

/// <summary>
/// Domain model for a game score (play result).
/// </summary>
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

    // Result statistics (300, 100, etc.)
    public Dictionary<string, int> Statistics { get; set; } = new();
    public List<string> Mods { get; set; } = new();

    public string BeatmapHash { get; set; } = string.Empty;
}

/// <summary>
/// Domain model for a game skin.
/// </summary>
public class GameSkin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<GameFileUsage> Files { get; set; } = new();
}

/// <summary>
/// Links a virtual filename to a physical content hash.
/// </summary>
public class GameFileUsage
{
    public string Filename { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty; // SHA-2 hash for VFS lookup
}

/// <summary>
/// Domain model for a user-created beatmap collection.
/// </summary>
public class GameCollection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> BeatmapHashes { get; set; } = new();
}
