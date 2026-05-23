using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Stable.Mappers;

/// <summary>
/// Maps OsuParsers beatmap objects (from osu!.db) to the abstract GameBeatmap model.
/// </summary>
public static class BeatmapMapper
{
    public static GameBeatmap Map(dynamic dbBeatmap)
    {
        return new GameBeatmap
        {
            Hash = dbBeatmap.MD5Hash ?? "",
            Md5Hash = dbBeatmap.MD5Hash ?? "",
            OnlineId = dbBeatmap.BeatmapId, // In osu!.db this is a plain int
            DifficultyName = dbBeatmap.Difficulty ?? "",
            
            Artist = dbBeatmap.ArtistUnicode ?? dbBeatmap.Artist ?? "",
            Title = dbBeatmap.TitleUnicode ?? dbBeatmap.Title ?? "",
            Creator = dbBeatmap.Creator ?? "",
            Source = dbBeatmap.Source ?? "",
            Tags = dbBeatmap.Tags ?? "",

            StarRating = GetDefaultStarRating(dbBeatmap),
            
            ApproachRate = (float)dbBeatmap.ApproachRate,
            CircleSize = (float)dbBeatmap.CircleSize,
            OverallDifficulty = (float)dbBeatmap.OverallDifficulty,
            DrainRate = (float)dbBeatmap.HPDrain,
            
            // Stable stores object counts in explicit fields
            CircleCount = (int)dbBeatmap.CirclesCount,
            SliderCount = (int)dbBeatmap.SlidersCount,
            SpinnerCount = (int)dbBeatmap.SpinnersCount,

            // Bpm can be in TimingPoints, we take the first one
            Bpm = (dbBeatmap.TimingPoints != null && dbBeatmap.TimingPoints.Count > 0) 
                  ? dbBeatmap.TimingPoints[0].BPM : 0,

            Mode = MapMode((int)dbBeatmap.Ruleset),
            BeatmapSetId = dbBeatmap.BeatmapSetId,
            
            AudioFile = dbBeatmap.AudioFileName ?? "",
            BackgroundFile = "", // Stable DB does not store background info directly
            FolderName = dbBeatmap.FolderName ?? "",
            
            RankedStatus = (int)dbBeatmap.RankedStatus,
            LastPlayed = dbBeatmap.LastPlayed
        };
    }

    private static float GetDefaultStarRating(dynamic dbBeatmap)
    {
        // Attempt to get difficulty for the default mode without mods (Mods.None = 0)
        try {
            switch ((int)dbBeatmap.Ruleset) {
                case 0: return (float)(dbBeatmap.StandardStarRating?[0] ?? 0);
                case 1: return (float)(dbBeatmap.TaikoStarRating?[0] ?? 0);
                case 2: return (float)(dbBeatmap.CatchStarRating?[0] ?? 0);
                case 3: return (float)(dbBeatmap.ManiaStarRating?[0] ?? 0);
            }
        } catch { }
        return 0f;
    }

    private static GameMode MapMode(int ruleset)
    {
        return ruleset switch
        {
            0 => GameMode.Osu,
            1 => GameMode.Taiko,
            2 => GameMode.Catch,
            3 => GameMode.Mania,
            _ => GameMode.Osu
        };
    }
}
