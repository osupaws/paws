using Paws.Core.Abstractions;

using Realms;
using Realms.Exceptions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Paws.Host
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
                // CRITICAL: We utilize dynamic access to avoid strict coupling to osu!lazer's internal schema.
                // Attempting to define a partial schema causes MigrationNeeded exceptions because Realm
                // expects the class definition to match the full table definition on disk.
                IsDynamic = true,
                // SchemaVersion removed to allow dynamic opening of any version
            };
        }

        /// <summary>
        /// Attempts to get a READ-ONLY instance of the Lazer database.
        /// Safe to use for checking state, but may fail if Lazer is performing exclusive maintenance.
        /// </summary>
        public Realm? GetSafeReadInstance()
        {
            var currentLazerPath = _pawsDbService.GetSetting("core.paths.lazer")?.Value;
            if (string.IsNullOrEmpty(currentLazerPath)) return null;

            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            if (!File.Exists(dbPath)) return null;

            try
            {
                var config = GetLazerConfig(dbPath, readOnly: true);
                return Realm.GetInstance(config);
            }
            catch (RealmPermissionDeniedException ex)
            {
                _logger.LogWarning("Lazer DB is currently locked by the game (permission denied): {Message}", ex.Message);
                return null;
            }
            catch (RealmException ex)
            {
                _logger.LogWarning("Lazer DB access error (possibly locked): {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open lazer database in Safe Read mode.");
                return null;
            }
        }

        /// <summary>
        /// Gets a WRITEABLE instance. STRICTLY checks if osu! is running first.
        /// </summary>
        public Realm? GetWriteableInstance()
        {
            var currentLazerPath = _pawsDbService.GetSetting("core.paths.lazer")?.Value;
            if (string.IsNullOrEmpty(currentLazerPath)) return null;

            // 1. Process Check
            string processName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "osu!" : "osu";
            if (Process.GetProcessesByName(processName).Any())
            {
                throw new LazerIsRunningException();
            }

            // 2. Lock File Check (Parity with Lazer's own checks)
            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            // Note: Realm will handle the lock file check internally, but explicit check
            // gives a better error if we want to be super safe.
            // For now, we rely on Process check + try/catch.

            if (!File.Exists(dbPath)) return null;

            try
            {
                var config = GetLazerConfig(dbPath, readOnly: false);
                return Realm.GetInstance(config);
            }
            catch (RealmMismatchedConfigException ex)
            {
                throw new LazerAccessConflictException(
                    "Cannot open Lazer database for writing because it is already open for reading in this process. " +
                    "Ensure you have disposed your LazerContext (wrapped in 'using') BEFORE calling PerformLazerWriteAsync.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open lazer database for writing.");
                return null;
            }
        }
    }
}

