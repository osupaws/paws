using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

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
