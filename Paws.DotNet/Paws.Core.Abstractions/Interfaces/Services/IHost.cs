using Paws.Core.Abstractions.Interfaces.Services;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    public interface IHost
    {
        ILogger Logger { get; }
        ILazerService Lazer { get; }
        IStableService Stable { get; }
        IStorageService Storage { get; }
        IImageProcessor Image { get; }
        bool IsLegacyMode { get; }
    }
}
