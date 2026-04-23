using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Paws.Abstractions.Models.Game;
using Paws.Drivers.Lazer.Mappers;
using Realms;

namespace Paws.Drivers.Lazer;

public class LazerDbService
{
    private readonly string _lazerPath;
    private string RealmPath => Path.Combine(_lazerPath, "client.realm");

    public LazerDbService(string lazerPath)
    {
        _lazerPath = lazerPath;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_lazerPath) && File.Exists(RealmPath);

    private Realm? GetRealm()
    {
        if (!IsAvailable) return null;
        try
        {
            var config = new RealmConfiguration(RealmPath)
            {
                IsReadOnly = true,
                IsDynamic = true
            };
            return Realm.GetInstance(config);
        }
        catch
        {
            return null;
        }
    }

    public IEnumerable<GameBeatmapSet> GetAllBeatmapSets()
    {
        using var realm = GetRealm();
        if (realm == null) return Array.Empty<GameBeatmapSet>();

        var list = new List<GameBeatmapSet>();
        var sets = realm.DynamicApi.All("BeatmapSet");
        
        foreach (dynamic set in sets)
        {
            try
            {
                // Мапим параметры сета
                var dto = new GameBeatmapSet
                {
                    OnlineId = set.OnlineID ?? -1,
                    FolderName = set.ID.ToString() ?? "",
                    Artist = set.Metadata?.Artist ?? "",
                    Title = set.Metadata?.Title ?? "",
                    Creator = set.Metadata?.Author?.Username ?? ""
                };
                
                // Мапим вложенные карты (если есть)
                if (set.Beatmaps != null)
                {
                    foreach (dynamic b in set.Beatmaps)
                    {
                        dto.Beatmaps.Add(BeatmapMapper.Map(b));
                    }
                }
                list.Add(dto);
            }
            catch { }
        }
        return list;
    }

    public IEnumerable<GameCollection> GetAllCollections()
    {
        using var realm = GetRealm();
        if (realm == null) return Array.Empty<GameCollection>();

        var list = new List<GameCollection>();
        try
        {
            var collections = realm.DynamicApi.All("BeatmapCollection");
            foreach (dynamic col in collections)
            {
                list.Add(CollectionMapper.Map(col));
            }
        }
        catch { }
        return list;
    }

    public IEnumerable<GameScore> GetScoresByBeatmapHash(string md5Hash)
    {
        using var realm = GetRealm();
        if (realm == null) return Array.Empty<GameScore>();

        var list = new List<GameScore>();
        try
        {
            var allScores = realm.DynamicApi.All("ScoreInfo");
            
            // В Realm dynamic API LINQ может не поддерживать вложенные свойства, 
            // поэтому фильтруем локально (записей рекордов может быть много, но это безопасно).
            foreach (dynamic score in allScores)
            {
                if (score.BeatmapInfo?.MD5Hash == md5Hash)
                {
                    list.Add(ScoreMapper.Map(score));
                }
            }
        }
        catch { }

        return list;
    }

    public IEnumerable<GameSkin> GetAllSkins()
    {
        using var realm = GetRealm();
        if (realm == null) return Array.Empty<GameSkin>();

        var list = new List<GameSkin>();
        try
        {
            var skins = realm.DynamicApi.All("SkinInfo");
            foreach (dynamic skin in skins)
            {
                list.Add(SkinMapper.Map(skin));
            }
        }
        catch { }
        
        return list;
    }
}
