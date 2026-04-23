using Paws.Abstractions.Models.Game;

namespace Paws.Drivers.Lazer.Mappers;

public static class RulesetMapper
{
    public static GameMode MapMode(int onlineId)
    {
        return onlineId switch
        {
            0 => GameMode.Osu,
            1 => GameMode.Taiko,
            2 => GameMode.Catch,
            3 => GameMode.Mania,
            _ => GameMode.Osu
        };
    }
}
