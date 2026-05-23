using System;
using System.Collections.Generic;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

/// <summary>
/// Maps Lazer Realm beatmap set objects to the abstract GameBeatmapSet model.
/// </summary>
public static class BeatmapSetMapper
{
    public static GameBeatmapSet Map(dynamic lazerSet)
    {
        var dto = new GameBeatmapSet
        {
            OnlineId = (long)(lazerSet.OnlineID ?? -1L),
            FolderName = lazerSet.ID.ToString() ?? "",
            Artist = lazerSet.Metadata?.Artist ?? "",
            Title = lazerSet.Metadata?.Title ?? "",
            Creator = lazerSet.Metadata?.Author?.Username ?? ""
        };

        if (lazerSet.Beatmaps != null)
        {
            foreach (dynamic b in lazerSet.Beatmaps)
            {
                dto.Beatmaps.Add(BeatmapMapper.Map(b));
            }
        }

        if (lazerSet.Files != null)
        {
            dto.Files.AddRange(FileMapper.MapList(lazerSet.Files));
        }

        return dto;
    }
}
