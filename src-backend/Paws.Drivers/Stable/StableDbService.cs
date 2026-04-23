using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OsuParsers.Database;
using OsuParsers.Decoders;
using Paws.Abstractions.Models.Game;
using Paws.Drivers.Stable.Mappers;

namespace Paws.Drivers.Stable;

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

            // В Stable нет явных 'моделей сетов', поэтому мы группируем карты по ID сета или имени папки
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
                    Beatmaps = group.ToList()
                });
            }

            return list;
        }
        catch
        {
            return Array.Empty<GameBeatmapSet>();
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
}
