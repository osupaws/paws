using Paws.Core.Abstractions;
using Realms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Paws.Host
{
    /// <summary>
    /// Manages the connection to the osu!lazer database file.
    /// </summary>
    public class LazerDbService
    {
        private readonly ILogger<LazerDbService> _logger;
        private readonly PawsDbService _pawsDbService;
        private string? _lazerDbPath; // This will now be primarily a cache/runtime value

        public LazerDbService(ILogger<LazerDbService> logger, PawsDbService pawsDbService)
        {
            _logger = logger;
            _pawsDbService = pawsDbService;
            _lazerDbPath = _pawsDbService.GetConfig().LazerPath; // Load path from config on startup
            _logger.LogInformation("Lazer path loaded from DB: {path}", _lazerDbPath ?? "Not set");
        }

        /// <summary>
        /// Sets the root path of the osu!lazer installation.
        /// </summary>
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
            _pawsDbService.SetConfig(config => config.LazerPath = path); // Persist path to DB
        }

        /// <summary>
        /// Gets a read-only dynamic instance of the lazer Realm database.
        /// </summary>
        /// <returns>A dynamic Realm instance or null if the path is not set/valid.</returns>
        public Realm? GetInstance()
        {
            // Always get the path from the persisted config
            var currentLazerPath = _pawsDbService.GetConfig().LazerPath;
            if (string.IsNullOrEmpty(currentLazerPath))
            {
                _logger.LogWarning("Attempted to get lazer DB instance, but path is not set in config.");
                return null;
            }

            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("client.realm not found at specified lazer path: {path}", currentLazerPath);
                return null;
            }

            try
            {
                // For a dynamic realm, you should NOT specify a schema.
                // The IsDynamic flag tells Realm to discover the schema from the file.
                var config = new RealmConfiguration(dbPath)
                {
                    IsDynamic = true,
                    IsReadOnly = true,
                };

                return Realm.GetInstance(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open lazer database at {path}", dbPath);
                return null;
            }
        }

        /// <summary>
        /// Gets a writeable dynamic instance of the lazer Realm database.
        /// </summary>
        /// <returns>A dynamic Realm instance or null if the path is not set/valid.</returns>
        /// <exception cref="LazerIsRunningException">Thrown if the osu!lazer process is detected.</exception>
        public Realm? GetWriteableInstance()
        {
            // Always get the path from the persisted config
            var currentLazerPath = _pawsDbService.GetConfig().LazerPath;
            if (string.IsNullOrEmpty(currentLazerPath))
            {
                _logger.LogWarning("Attempted to get writeable lazer DB instance, but path is not set in config.");
                return null;
            }

            // Check if lazer is running before attempting to get a write lock.
            string processName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "osu!" : "osu";
            if (Process.GetProcessesByName(processName).Any())
            {
                throw new LazerIsRunningException();
            }

            var dbPath = Path.Combine(currentLazerPath, "client.realm");
            if (!File.Exists(dbPath))
            {
                _logger.LogWarning("client.realm not found at specified lazer path: {path}", currentLazerPath);
                return null;
            }

            try
            {
                // The same configuration applies here for the writeable instance.
                // No schema should be specified.
                var config = new RealmConfiguration(dbPath)
                {
                    IsDynamic = true,
                    IsReadOnly = false,
                };

                return Realm.GetInstance(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open lazer database for writing at {path}", dbPath);
                return null;
            }
        }
    }
}