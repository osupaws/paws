using System;
using System.Collections.Generic;
using System.Text.Json;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

/// <summary>
/// Maps Lazer Realm score objects to the abstract GameScore model.
/// </summary>
public static class ScoreMapper
{
    public static GameScore Map(dynamic lazerScore)
    {
        var statistics = new Dictionary<string, int>();
        var mods = new List<string>();

        if (lazerScore.StatisticsJson != null)
        {
            try { statistics = JsonSerializer.Deserialize<Dictionary<string, int>>(lazerScore.StatisticsJson); } catch {}
        }

        if (lazerScore.ModsJson != null)
        {
            try { mods = JsonSerializer.Deserialize<List<string>>(lazerScore.ModsJson); } catch {}
        }

        return new GameScore
        {
            Id = lazerScore.ID,
            OnlineId = lazerScore.OnlineID ?? -1,
            PlayerName = lazerScore.RealmUser?.Username ?? "Unknown",
            TotalScore = lazerScore.TotalScore ?? 0,
            Accuracy = (float)(lazerScore.Accuracy ?? 0f),
            MaxCombo = (int)(lazerScore.MaxCombo ?? 0),
            Rank = lazerScore.RankInt?.ToString() ?? "N/A",
            Date = lazerScore.Date,
            PP = (double?)lazerScore.PP,
            
            Statistics = statistics ?? new(),
            Mods = mods ?? new(),
            
            BeatmapHash = lazerScore.BeatmapInfo?.MD5Hash ?? ""
        };
    }
}
