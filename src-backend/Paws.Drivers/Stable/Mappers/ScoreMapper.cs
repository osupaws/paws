using System;
using System.Collections.Generic;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Stable.Mappers;

public static class ScoreMapper
{
    public static GameScore Map(dynamic dbScore)
    {
        return new GameScore
        {
            Id = Guid.NewGuid(), // В Stable ScoresDatabase нет Guid, только онлайн-ID
            OnlineId = dbScore.ScoreId,
            PlayerName = dbScore.PlayerName ?? "Unknown",
            TotalScore = dbScore.ReplayScore,
            Accuracy = 0f, // Точность в Stable .db не хранится, её нужно считать из хитов
            MaxCombo = dbScore.Combo,
            Rank = dbScore.Rank?.ToString() ?? "N/A",
            Date = dbScore.DateTime.ToDateTime(), // OsuParsers использует ReplayTimestamp
            PP = null, // PP в Stable .db не хранится
            
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
            
            // Моды в Stable — это Bitwise enum
            Mods = MapMods((long)dbScore.Mods),
            
            BeatmapHash = dbScore.BeatmapMD5Hash ?? ""
        };
    }

    private static List<string> MapMods(long mods)
    {
        // Пока мы не будем делать полный список модов, 
        // но оставим заглушку для будущей реализации парсинга флагов.
        return new List<string> { mods.ToString() }; 
    }
}
