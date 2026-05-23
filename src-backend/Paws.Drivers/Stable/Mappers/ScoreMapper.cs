using System;
using System.Collections.Generic;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Stable.Mappers;

/// <summary>
/// Maps OsuParsers score objects (from scores.db) to the abstract GameScore model.
/// </summary>
public static class ScoreMapper
{
    public static GameScore Map(dynamic dbScore)
    {
        return new GameScore
        {
            Id = Guid.NewGuid(), // Stable ScoresDatabase doesn't have Guids, only online IDs
            OnlineId = dbScore.ScoreId,
            PlayerName = dbScore.PlayerName ?? "Unknown",
            TotalScore = dbScore.ReplayScore,
            Accuracy = 0f, // Accuracy is not stored in Stable .db, must be calculated from hits
            MaxCombo = dbScore.Combo,
            Rank = dbScore.Rank?.ToString() ?? "N/A",
            Date = dbScore.DateTime.ToDateTime(), // OsuParsers использует ReplayTimestamp
            PP = null, // PP is not stored in Stable .db
            
            // Маппинг хитов
            Statistics = new Dictionary<string, int>
            {
                { "Count300", dbScore.Count300 },
                { "Count100", dbScore.Count100 },
                { "Count50", dbScore.Count50 },
                { "CountMiss", dbScore.CountMiss },
                { "CountGeki", dbScore.CountGeki },
                { "CountKatu", dbScore.CountKatu }
            },
            
            // Mods in Stable are a Bitwise enum
            Mods = MapMods((long)dbScore.Mods),
            
            BeatmapHash = dbScore.BeatmapMD5Hash ?? ""
        };
    }

    private static List<string> MapMods(long mods)
    {
        // We don't implement full mod parsing yet, but leaving a stub for the future.
        return new List<string> { mods.ToString() }; 
    }
}
