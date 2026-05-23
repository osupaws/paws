using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OsuParsers.Database;
using OsuParsers.Decoders;
using OsuParsers.Encoders;
using Paws.Abstractions.Models.Game;
using Paws.Drivers.Stable.Mappers;

namespace Paws.Drivers.Stable;

/// <summary>
/// Driver for interacting with osu!stable databases (osu!.db, collection.db, scores.db).
/// Uses OsuParsers for binary decoding/encoding.
/// </summary>
public class StableDbService
{
    private readonly string _stablePath;

    public StableDbService(string stablePath)
    {
        _stablePath = stablePath;
    }

    private string OsuDbPath => Path.Combine(_stablePath, "osu!.db");
    private string CollectionDbPath => Path.Combine(_stablePath, "collection.db");
    private string ScoresDbPath => Path.Combine(_stablePath, "scores.db");

    public bool IsAvailable => !string.IsNullOrEmpty(_stablePath) && File.Exists(OsuDbPath);

    public IEnumerable<GameBeatmapSet> GetAllBeatmapSets()
    {
        if (!IsAvailable) return Array.Empty<GameBeatmapSet>();

        try
        {
            var osuDb = DatabaseDecoder.DecodeOsu(OsuDbPath);
            var mappedBeatmaps = osuDb.Beatmaps.Select(b => BeatmapMapper.Map(b));

            // Stable doesn't have explicit 'BeatmapSet' objects, so we group maps by SetID or FolderName.
            var grouped = mappedBeatmaps.GroupBy(b => string.IsNullOrEmpty(b.FolderName) ? b.BeatmapSetId.ToString() : b.FolderName);

            var list = new List<GameBeatmapSet>();
            foreach (var group in grouped)
            {
                var first = group.First();
                list.Add(new GameBeatmapSet
                {
                    OnlineId = first.BeatmapSetId,
                    FolderName = first.FolderName,
                    Artist = first.Artist,
                    Title = first.Title,
                    Creator = first.Creator,
                    Beatmaps = group.ToList(),
                    Files = ListStableSetFiles(first.FolderName)
                });
            }

            return list;
        }
        catch
        {
            return Array.Empty<GameBeatmapSet>();
        }
    }

    public GameBeatmap? GetBeatmapByHash(string md5Hash)
    {
        try
        {
            var osuDb = DatabaseDecoder.DecodeOsu(OsuDbPath);
            var map = osuDb.Beatmaps.FirstOrDefault(b => b.MD5Hash == md5Hash);
            return map != null ? BeatmapMapper.Map(map) : null;
        }
        catch { return null; }
    }

    public bool DeleteRecord(string type, string id)
    {
        if (type != "BeatmapInfo") return false; // Currently only beatmaps supported

        try
        {
            var osuDb = DatabaseDecoder.DecodeOsu(OsuDbPath);
            var initialCount = osuDb.BeatmapCount;
            
            osuDb.Beatmaps.RemoveAll(b => b.MD5Hash == id);
            
            if (osuDb.BeatmapCount < initialCount)
            {
                osuDb.Save(OsuDbPath);
                Console.WriteLine($"[StableDb] Deleted beatmap with hash {id} from osu!.db");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StableDb] Delete failed: {ex.Message}");
            return false;
        }
    }

    public IEnumerable<GameCollection> GetAllCollections()
    {
        if (!File.Exists(CollectionDbPath)) return Array.Empty<GameCollection>();

        try
        {
            var collectionDb = DatabaseDecoder.DecodeCollection(CollectionDbPath);
            return collectionDb.Collections.Select(c => CollectionMapper.Map(c));
        }
        catch
        {
            return Array.Empty<GameCollection>();
        }
    }

    public IEnumerable<GameScore> GetScoresByBeatmapHash(string md5Hash)
    {
        if (!File.Exists(ScoresDbPath)) return Array.Empty<GameScore>();

        try
        {
            var scoresDb = DatabaseDecoder.DecodeScores(ScoresDbPath);
            var beatmapScores = scoresDb.Scores.FirstOrDefault(s => s.Item1 == md5Hash);
            
            if (beatmapScores?.Item2 != null)
            {
                return beatmapScores.Item2.Select(s => ScoreMapper.Map(s));
            }
        }
        catch { }

        return Array.Empty<GameScore>();
    }

    private List<GameFileUsage> ListStableSetFiles(string folderName)
    {
        var list = new List<GameFileUsage>();
        if (string.IsNullOrEmpty(folderName)) return list;

        var fullPath = Path.Combine(_stablePath, "Songs", folderName);
        if (!Directory.Exists(fullPath)) return list;

        foreach (var file in Directory.GetFiles(fullPath))
        {
            list.Add(new GameFileUsage
            {
                Filename = Path.GetFileName(file),
                Hash = "" // Stable files don't use content-addressable storage (hashing is overkill here)
            });
        }
        return list;
    }
}
