using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Paws.Abstractions.Models.Game;
using Paws.Drivers.Lazer.Mappers;
using Realms;

namespace Paws.Drivers.Lazer;

/// <summary>
/// Low-level driver for interacting with the osu!lazer Realm database.
/// Uses dynamic Realm API to avoid schema coupling.
/// </summary>
public class LazerDbService
{
    private readonly string _lazerPath;
    private string RealmPath => Path.Combine(_lazerPath, "client.realm");

    public LazerDbService(string lazerPath)
    {
        _lazerPath = lazerPath;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_lazerPath) && File.Exists(RealmPath);

    private Realm? GetRealm(bool writeable = false)
    {
        if (!IsAvailable) return null;
        try
        {
            var config = new RealmConfiguration(RealmPath)
            {
                IsReadOnly = !writeable,
                IsDynamic = true
            };
            return Realm.GetInstance(config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LazerDb] Failed to open realm (writeable={writeable}): {ex.Message}");
            return null;
        }
    }

    public bool DeleteRecord(string type, string id)
    {
        using var realm = GetRealm(true);
        if (realm == null) return false;

        try
        {
            realm.Write(() =>
            {
                dynamic? obj = null;
                if (Guid.TryParse(id, out var guid)) 
                    obj = realm.DynamicApi.Find(type, (Guid?)guid);
                else if (long.TryParse(id, out var longId)) 
                    obj = realm.DynamicApi.Find(type, (long?)longId);
                else 
                    obj = realm.DynamicApi.Find(type, id);

                if (obj != null)
                {
                    realm.Remove(obj);
                    Console.WriteLine($"[LazerDb] Deleted {type} with ID {id}");
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LazerDb] Delete failed: {ex.Message}");
            return false;
        }
    }

    public bool UpdateRecord(string type, string id, object data)
    {
        using var realm = GetRealm(true);
        if (realm == null) return false;

        try
        {
            realm.Write(() =>
            {
                dynamic? obj = null;
                if (Guid.TryParse(id, out var guid)) 
                    obj = realm.DynamicApi.Find(type, (Guid?)guid);
                else if (long.TryParse(id, out var longId)) 
                    obj = realm.DynamicApi.Find(type, (long?)longId);
                else 
                    obj = realm.DynamicApi.Find(type, id);

                if (obj != null)
                {
                    // Basic update logic: we expect 'data' to be a Dictionary or similar from JSON RPC
                    if (data is System.Text.Json.JsonElement json)
                    {
                        foreach (var prop in json.EnumerateObject())
                        {
                            try { obj[prop.Name] = prop.Value.GetString(); } catch { }
                        }
                    }
                    Console.WriteLine($"[LazerDb] Updated {type} with ID {id}");
                }
            });
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LazerDb] Update failed: {ex.Message}");
            return false;
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
            if (set.DeletePending == true) continue;

            try
            {
                list.Add(BeatmapSetMapper.Map(set));
            }
            catch { }
        }
        return list;
    }

    public GameBeatmap? GetBeatmapByHash(string md5Hash)
    {
        using var realm = GetRealm();
        if (realm == null) return null;

        try
        {
            var beatmap = realm.DynamicApi.All("BeatmapInfo")
                .Filter("MD5Hash == $0", md5Hash)
                .FirstOrDefault();
            
            return beatmap != null ? BeatmapMapper.Map(beatmap) : null;
        }
        catch { return null; }
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
            foreach (dynamic score in allScores)
            {
                if (score.DeletePending == true) continue;
                
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
