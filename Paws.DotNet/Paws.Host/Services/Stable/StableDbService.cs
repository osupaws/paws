using OsuParsers.Database;
using OsuParsers.Decoders;
using System.Threading.Tasks;
using Paws.Host.Services.Core;
using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Stable
{
    public class StableDbService
    {
        private readonly ILogger<StableDbService> _logger;
        private readonly PawsDbService _pawsDbService;
        private string? _stableRootPath;

        private OsuDatabase? _cachedOsuDb;
        private ScoresDatabase? _cachedScoresDb;

        private DateTime _osuDbCacheTimestamp;
        private DateTime _scoresDbCacheTimestamp;

        public StableDbService(ILogger<StableDbService> logger, PawsDbService pawsDbService)
        {
            _logger = logger;
            _pawsDbService = pawsDbService;
            _stableRootPath = _pawsDbService.GetSetting("core.paths.stable")?.Value;

            if (string.IsNullOrEmpty(_stableRootPath))
            {
                var detectedPath = ResolveStablePath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _pawsDbService.SetSetting("core.paths.stable", detectedPath);
                    _stableRootPath = detectedPath;
                    _logger.LogInformation("Stable path auto-detected and saved: {path}", detectedPath);
                }
            }

            _logger.LogInformation("Stable path resolved: {path}", _stableRootPath ?? "Not set");
        }

        public void SetStablePath(string path)
        {
            _stableRootPath = path;
            _cachedOsuDb = null;
            _cachedScoresDb = null;
            _pawsDbService.SetSetting("core.paths.stable", path);
        }

        public string? ResolveStablePath()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return null;

            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!");
            if (File.Exists(Path.Combine(defaultPath, "osu!.exe")))
                return defaultPath;

            return null;
        }

        public string? GetStableRootPath()
        {
            var saved = _pawsDbService.GetSetting("core.paths.stable")?.Value;
            if (!string.IsNullOrEmpty(saved)) return saved;

            return ResolveStablePath();
        }

        public async Task<OsuDatabase?> GetOsuDbAsync()
        {
            var result = await GetOrParseDbAsync("osu!.db", _cachedOsuDb, _osuDbCacheTimestamp, (path) => Task.Run(() => DatabaseDecoder.DecodeOsu(path)));
            if (result.HasValue)
            {
                _cachedOsuDb = result.Value.data;
                _osuDbCacheTimestamp = result.Value.timestamp;
            }
            return _cachedOsuDb;
        }

        public async Task<ScoresDatabase?> GetScoresDbAsync()
        {
            var result = await GetOrParseDbAsync("scores.db", _cachedScoresDb, _scoresDbCacheTimestamp, (path) => Task.Run(() => DatabaseDecoder.DecodeScores(path)));
            if (result.HasValue)
            {
                _cachedScoresDb = result.Value.data;
                _scoresDbCacheTimestamp = result.Value.timestamp;
            }
            return _cachedScoresDb;
        }

        private async Task<(T? data, DateTime timestamp)?> GetOrParseDbAsync<T>(string fileName, T? cacheField, DateTime cacheTimestamp, Func<string, Task<T>> parseFunc) where T : class
        {
            var root = GetStableRootPath();
            if (string.IsNullOrEmpty(root)) return null;

            var dbPath = Path.Combine(root, fileName);
            if (!File.Exists(dbPath)) return null;

            var lastWriteTime = File.GetLastWriteTimeUtc(dbPath);
            if (cacheField != null && lastWriteTime <= cacheTimestamp) return (cacheField, cacheTimestamp);

            try
            {
                var parsedData = await parseFunc(dbPath);
                return (parsedData, lastWriteTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse {FileName}.", fileName);
                return null;
            }
        }

        public static bool IsStableRunning()
        {
            try
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    return false; // Stable is Windows only

                var processes = System.Diagnostics.Process.GetProcessesByName("osu!");
                var lazerTargetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer", "current");

                // If it's osu!.exe but NOT in lazer directory, it's stable
                return processes.Any(p =>
                {
                    try { return !(p.MainModule?.FileName?.StartsWith(lazerTargetDir, StringComparison.OrdinalIgnoreCase) ?? false); }
                    catch { return false; }
                });
            }
            catch { return false; }
        }
    }
}
