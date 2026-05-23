using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

/// <summary>
/// Maps Lazer Realm collection objects to the abstract GameCollection model.
/// </summary>
public static class CollectionMapper
{
    public static GameCollection Map(dynamic lazerCollection)
    {
        var hashes = new List<string>();
        if (lazerCollection.BeatmapMD5Hashes != null)
        {
            foreach (var hash in lazerCollection.BeatmapMD5Hashes)
                hashes.Add(hash ?? "");
        }

        return new GameCollection
        {
            Id = lazerCollection.ID,
            Name = lazerCollection.Name ?? "Untitled",
            BeatmapHashes = hashes
        };
    }
}
