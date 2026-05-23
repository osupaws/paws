using System;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

/// <summary>
/// Maps Lazer Realm beatmap objects to the abstract GameBeatmap model.
/// </summary>
public static class BeatmapMapper
{
    public static GameBeatmap Map(dynamic lazerBeatmap)
    {
        var metadata = lazerBeatmap.Metadata;
        var diff = lazerBeatmap.Difficulty;
        var set = lazerBeatmap.BeatmapSet;

        return new GameBeatmap
        {
            Hash = lazerBeatmap.MD5Hash ?? "",
            Md5Hash = lazerBeatmap.MD5Hash ?? "",
            OnlineId = lazerBeatmap.OnlineID ?? -1,
            DifficultyName = lazerBeatmap.DifficultyName ?? "",
            
            Artist = metadata?.Artist ?? "",
            Title = metadata?.Title ?? "",
            Creator = metadata?.Author?.Username ?? "",
            Source = metadata?.Source ?? "",
            Tags = metadata?.Tags ?? "",

            StarRating = (float)(lazerBeatmap.StarRating ?? 0f),
            
            ApproachRate = (float)(diff?.ApproachRate ?? 5.0f),
            CircleSize = (float)(diff?.CircleSize ?? 5.0f),
            OverallDifficulty = (float)(diff?.OverallDifficulty ?? 5.0f),
            DrainRate = (float)(diff?.DrainRate ?? 5.0f),
            
            Mode = RulesetMapper.MapMode(lazerBeatmap.Ruleset?.OnlineID ?? 0),
            BeatmapSetId = set?.OnlineID ?? -1,
            
            AudioFile = metadata?.AudioFile ?? "",
            BackgroundFile = metadata?.BackgroundFile ?? "",
            FolderName = set?.ID.ToString() ?? "",
            
            RankedStatus = (int)(set?.Status ?? 0),
            LastPlayed = lazerBeatmap.LastPlayed
        };
    }
}
