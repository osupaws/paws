using Paws.Core.Abstractions.Interfaces.Services;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    public interface IHost : ILogger, ILazerService, IStableService
    {
        bool IsLegacyMode { get; }
    }
}
