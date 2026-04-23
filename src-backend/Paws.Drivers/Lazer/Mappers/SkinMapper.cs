using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

public static class SkinMapper
{
    public static GameSkin Map(dynamic lazerSkin)
    {
        return new GameSkin
        {
            Id = lazerSkin.ID,
            Name = lazerSkin.Name ?? "Default",
            Creator = lazerSkin.Creator ?? "osu!",
            IsDefault = (bool)(lazerSkin.Protected ?? false),
            Files = FileMapper.MapList(lazerSkin.Files)
        };
    }
}
