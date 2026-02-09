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
            _logger.LogInformation("Stable path loaded from DB: {path}", _stableRootPath ?? "Not set");
        }

        public void SetStablePath(string path)
        {
            _stableRootPath = path;
            _cachedOsuDb = null;
            _cachedScoresDb = null;
            _pawsDbService.SetSetting("core.paths.stable", path);
        }

        public string? GetStableRootPath() => _pawsDbService.GetSetting("core.paths.stable")?.Value;

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
    }
}
