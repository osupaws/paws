using Paws.Core.Abstractions.Exceptions;
using Paws.Host.Data.Schemas;
using Paws.Host.Services.Core;
using Realms;
using Realms.Exceptions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Lazer
{
    /// <summary>
    /// Manages the connection to the osu!lazer database file using a minimal viable schema (MVS).
    /// </summary>
    public class LazerDbService
    {
        private readonly ILogger<LazerDbService> _logger;
        private readonly PawsDbService _pawsDbService;
        private string? _lazerDbPath; // Cached path

        public LazerDbService(ILogger<LazerDbService> logger, PawsDbService pawsDbService)
        {
            _logger = logger;
            _pawsDbService = pawsDbService;
            _lazerDbPath = _pawsDbService.GetSetting("core.paths.lazer")?.Value;
            _logger.LogInformation("Lazer path loaded from DB: {path}", _lazerDbPath ?? "Not set");
        }

        public string? GetLazerBasePath()
        {
            return _pawsDbService.GetSetting("core.paths.lazer")?.Value;
        }

        public void SetLazerPath(string path)
        {
            var dbPath = Path.Combine(path, "client.realm");
            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("client.realm not found at specified lazer path: {path}", path);
                _lazerDbPath = null;
                return;
            }

            _logger.LogInformation("Lazer database path set to: {dbPath}", dbPath);
            _lazerDbPath = dbPath;
            _pawsDbService.SetSetting("core.paths.lazer", path);
        }

        private RealmConfiguration GetLazerConfig(string dbPath, bool readOnly)
        {
            return new RealmConfiguration(dbPath)
            {
                IsReadOnly = readOnly,
                IsDynamic = true
            };
        }

        public Realm? GetSafeReadInstance()
        {
            var currentLazerPath = GetLazerBasePath();
            if (string.IsNullOrEmpty(currentLazerPath)) return null;

            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            if (!File.Exists(dbPath)) return null;

            try
            {
                var config = GetLazerConfig(dbPath, readOnly: true);
                return Realm.GetInstance(config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lazer DB access error: {Message}", ex.Message);
                return null;
            }
        }

        public Realm? GetWriteableInstance()
        {
            var currentLazerPath = GetLazerBasePath();
            if (string.IsNullOrEmpty(currentLazerPath)) return null;

            string processName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "osu!" : "osu";
            if (Process.GetProcessesByName(processName).Any())
            {
                throw new LazerIsRunningException();
            }

            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            if (!File.Exists(dbPath)) return null;

            try
            {
                var config = GetLazerConfig(dbPath, readOnly: false);
                return Realm.GetInstance(config);
            }
            catch (RealmMismatchedConfigException ex)
            {
                throw new LazerAccessConflictException("Conflict with open read-only stream", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open lazer database for writing.");
                return null;
            }
        }
    }
}
