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

            // Resolve path on startup if not explicitly set
            _lazerDbPath = _pawsDbService.GetSetting("core.paths.lazer")?.Value;

            if (string.IsNullOrEmpty(_lazerDbPath))
            {
                var detectedPath = ResolveLazerDataPath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _pawsDbService.SetSetting("core.paths.lazer", detectedPath);
                    _lazerDbPath = detectedPath;
                    _logger.LogInformation("Lazer path auto-detected and saved: {path}", detectedPath);
                }
            }

            _logger.LogInformation("Lazer path resolved: {path}", _lazerDbPath ?? "Not set");
        }

        public string? ResolveLazerDataPath()
        {
            string anchor;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                anchor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                anchor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support", "osu");
            else
                anchor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "osu");

            if (!Directory.Exists(anchor)) return null;

            var iniPath = Path.Combine(anchor, "storage.ini");
            if (File.Exists(iniPath))
            {
                try
                {
                    var lines = File.ReadAllLines(iniPath);
                    foreach (var line in lines)
                    {
                        if (line.Trim().StartsWith("FullPath", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                var resolvedPath = parts[1].Trim();
                                if (Directory.Exists(resolvedPath)) return resolvedPath;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to read storage.ini at {path}: {ex}", iniPath, ex.Message);
                }
            }

            // Fallback to anchor if no storage.ini or it's invalid
            return File.Exists(Path.Combine(anchor, "client.realm")) ? anchor : null;
        }

        public string? GetLazerBasePath()
        {
            var saved = _pawsDbService.GetSetting("core.paths.lazer")?.Value;
            if (!string.IsNullOrEmpty(saved)) return saved;

            return ResolveLazerDataPath();
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

            if (IsLazerRunning())
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

        public static bool IsLazerRunning()
        {
            try
            {
                string processName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "osu!" : "osu";
                var processes = Process.GetProcessesByName(processName);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var lazerTargetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer", "current");
                    return processes.Any(p =>
                    {
                        try { return p.MainModule?.FileName?.StartsWith(lazerTargetDir, StringComparison.OrdinalIgnoreCase) ?? false; }
                        catch { return false; }
                    });
                }

                return processes.Any(); // On other platforms we don't have a reliable "current" dir check yet
            }
            catch { return false; }
        }
    }
}
