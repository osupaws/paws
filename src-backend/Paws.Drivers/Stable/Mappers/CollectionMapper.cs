using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Stable.Mappers;

/// <summary>
/// Maps OsuParsers collection objects (from collection.db) to the abstract GameCollection model.
/// </summary>
public static class CollectionMapper
{
    // OsuParsers uses Collection objects
    public static GameCollection Map(dynamic dbCollection)
    {
        return new GameCollection
        {
            Id = Guid.NewGuid(), // Stable collections.db doesn't have Guids
            Name = dbCollection.Name ?? "Untitled",
            BeatmapHashes = dbCollection.MD5Hashes ?? new List<string>()
        };
    }
}
