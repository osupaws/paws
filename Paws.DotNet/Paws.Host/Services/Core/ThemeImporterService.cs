using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Core
{
    public class ThemeImporterService
    {
        private readonly ILogger<ThemeImporterService> _logger;
        private readonly PawsDbService _dbService;
        private readonly FileStorageService _storage;

        public ThemeImporterService(ILogger<ThemeImporterService> logger, PawsDbService dbService, FileStorageService storage)
        {
            _logger = logger;
            _dbService = dbService;
            _storage = storage;
        }

        public async Task<object?> ImportThemeAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Theme archive not found.", zipPath);

            using var archive = ZipFile.OpenRead(zipPath);
            var manifestEntry = archive.GetEntry("theme.json");
            if (manifestEntry == null) throw new InvalidDataException("theme.json missing from archive.");

            using var reader = new StreamReader(manifestEntry.Open());
            var manifestJson = await reader.ReadToEndAsync();

            // For now, we just return the manifest as a demonstration.
            // Actual theme registration in DB would go here.
            return JsonSerializer.Deserialize<object>(manifestJson);
        }
    }
}
