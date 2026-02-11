using Paws.Core.Abstractions.Interfaces.Services;

namespace Paws.Host.Services.Core
{
    /// <summary>
    /// A contextual wrapper for IHost that provides plugin-specific service instances (like Storage).
    /// </summary>
    public class PluginHost : Paws.Core.Abstractions.Interfaces.Services.IHost
    {
        private readonly System.Func<bool> _isLegacyProvider;

        public Paws.Core.Abstractions.Interfaces.Services.ILogger Logger { get; }
        public Paws.Core.Abstractions.Interfaces.Services.ILazerService Lazer { get; }
        public Paws.Core.Abstractions.Interfaces.Services.IStableService Stable { get; }
        public Paws.Core.Abstractions.Interfaces.Services.IStorageService Storage { get; }
        public Paws.Core.Abstractions.Interfaces.Services.IImageProcessor Image { get; }
        public bool IsLegacyMode => _isLegacyProvider();

        public PluginHost(
            Paws.Core.Abstractions.Interfaces.Services.ILogger logger,
            Paws.Core.Abstractions.Interfaces.Services.ILazerService lazer,
            Paws.Core.Abstractions.Interfaces.Services.IStableService stable,
            Paws.Core.Abstractions.Interfaces.Services.IStorageService storage,
            Paws.Core.Abstractions.Interfaces.Services.IImageProcessor image,
            System.Func<bool> isLegacyProvider)
        {
            Logger = logger;
            Lazer = lazer;
            Stable = stable;
            Storage = storage;
            Image = image;
            _isLegacyProvider = isLegacyProvider;
        }
    }
}
