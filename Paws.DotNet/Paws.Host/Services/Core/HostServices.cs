using Paws.Core.Abstractions.Interfaces.Services;
using PawsHost = Paws.Core.Abstractions.Interfaces.Services.IHost;
using PawsLogger = Paws.Core.Abstractions.Interfaces.Services.ILogger;
using Paws.Core.Abstractions.Interfaces.Contexts;
using Paws.Core.Abstractions.Enums;
using Paws.Core.Abstractions.Exceptions;
using Paws.Host.Services.Lazer;
using Paws.Host.Services.Stable;
using Realms;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;

namespace Paws.Host.Services.Core
{
    public class HostServices : PawsHost, ILazerService, IStableService, PawsLogger
    {
        private readonly ILogger<HostServices> _aspLogger;
        private readonly LazerDbService _lazerDbService;
        private readonly StableDbService _stableDbService;
        private readonly PawsDbService _pawsDbService;
        private readonly IStorageService _storage;
        private readonly IImageProcessor _image;

        public HostServices(
            ILogger<HostServices> logger,
            LazerDbService lazerDbService,
            StableDbService stableDbService,
            PawsDbService pawsDbService,
            IStorageService storage,
            IImageProcessor image)
        {
            _aspLogger = logger;
            _lazerDbService = lazerDbService;
            _stableDbService = stableDbService;
            _pawsDbService = pawsDbService;
            _storage = storage;
            _image = image;
        }

        // IHost explicit implementation
        Paws.Core.Abstractions.Interfaces.Services.ILogger PawsHost.Logger => this;
        ILazerService PawsHost.Lazer => this;
        IStableService PawsHost.Stable => this;
        IStorageService PawsHost.Storage => _storage;
        IImageProcessor PawsHost.Image => _image;

        public bool IsLegacyMode => _pawsDbService.GetSetting("core.modes.legacy")?.Value == "true";

        // ILogger logic
        public void LogMessage(string message, PawsLogLvl level = PawsLogLvl.Information, string? pluginName = null)
        {
            string prefix = pluginName != null ? $"[{pluginName}] " : "";
            _aspLogger.Log((Microsoft.Extensions.Logging.LogLevel)level, "{Prefix}{Message}", prefix, message);
        }

        public void LogProgress(string message, double percent)
        {
            _aspLogger.LogInformation("[Progress] {Percent:0.0}% - {Message}", percent, message);
        }

        // ILazerService logic
        public ILazerContext? GetLazerContext() => new LazerContext(_lazerDbService, _aspLogger);
        public dynamic? GetLazerDatabase() => _lazerDbService.GetSafeReadInstance();
        public Task PerformLazerWriteAsync(Action<Realm> action)
        {
            return Task.Run(() =>
            {
                using var db = _lazerDbService.GetWriteableInstance();
                if (db == null) throw new InvalidOperationException("Failed to open the lazer database for writing.");
                using var transaction = db.BeginWrite();
                action(db);
                transaction.Commit();
            });
        }

        // IStableService logic
        public async Task<object?> GetStableOsuDbAsync() => await _stableDbService.GetOsuDbAsync();
        public async Task<object?> GetStableScoresDbAsync() => await _stableDbService.GetScoresDbAsync();
        public Task PerformStableWriteAsync(Action<string> action)
        {
            return Task.Run(() =>
            {
                var stablePath = _stableDbService.GetStableRootPath();
                if (string.IsNullOrEmpty(stablePath)) throw new InvalidOperationException("osu!stable path is not set.");
                if (Process.GetProcessesByName("osu!").Any()) throw new StableIsRunningException();
                action(stablePath);
            });
        }
        public IStableContext GetStableContext() => new StableContext(_stableDbService.GetStableRootPath());
    }
}
