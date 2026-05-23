using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

/// <summary>
/// Maps Lazer Realm file usage objects to the abstract GameFileUsage model.
/// </summary>
public static class FileMapper
{
    public static GameFileUsage Map(dynamic lazerFileUsage)
    {
        return new GameFileUsage
        {
            Filename = lazerFileUsage.Filename ?? "",
            Hash = lazerFileUsage.File?.Hash ?? ""
        };
    }

    public static List<GameFileUsage> MapList(dynamic lazerFiles)
    {
        var list = new List<GameFileUsage>();
        if (lazerFiles == null) return list;

        foreach (var file in lazerFiles)
        {
            list.Add(Map(file));
        }
        return list;
    }
}
