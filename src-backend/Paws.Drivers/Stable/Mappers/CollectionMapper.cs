using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Stable.Mappers;

public static class CollectionMapper
{
    // OsuParsers использует объекты Collection
    public static GameCollection Map(dynamic dbCollection)
    {
        return new GameCollection
        {
            Id = Guid.NewGuid(), // В Stable collections.db нет Guid
            Name = dbCollection.Name ?? "Untitled",
            BeatmapHashes = dbCollection.MD5Hashes ?? new List<string>()
        };
    }
}
