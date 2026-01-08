using Paws.Host.Data;
using Paws.Host.Data.Schemas;
using Realms; // Required for Freeze() extension method
using System.IO.Compression;
using System.Text.Json;

namespace Paws.Host
{
    public class ThemeImporterService
    {
        private readonly ILogger<ThemeImporterService> _logger;
        private readonly FileStorageService _storage;
        private readonly PawsDbService _db;

        // Define a record to represent the structure of theme.json
        private record ThemeManifest(string Id, string Name, string Base, string File, string? Author, string? Version);

        public ThemeImporterService(ILogger<ThemeImporterService> logger, FileStorageService storage, PawsDbService db)
        {
            _logger = logger;
            _storage = storage;
            _db = db;
        }

        public async Task<ThemeDto> ImportThemeAsync(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Theme file not found.", zipFilePath);
            }

            using var archive = ZipFile.OpenRead(zipFilePath);
            
            var manifestEntry = archive.GetEntry("theme.json");
            if (manifestEntry == null)
            {
                throw new InvalidDataException("Theme archive must contain a 'theme.json' file.");
            }

            await using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<ThemeManifest>(manifestStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null)
            {
                throw new InvalidDataException("Failed to parse 'theme.json'.");
            }

            var cssEntry = archive.GetEntry(manifest.File);
            if (cssEntry == null)
            {
                throw new InvalidDataException($"CSS file '{manifest.File}' specified in theme.json was not found in the archive.");
            }

            await using var cssStream = new MemoryStream();
            await cssEntry.Open().CopyToAsync(cssStream);
            var cssBytes = cssStream.ToArray();

            var cssHash = await _storage.StoreFileAsync(cssBytes);
            _logger.LogInformation("Stored theme CSS file with hash: {hash}", cssHash);

            // Получаем конфигурацию от сервиса, но управляем экземпляром Realm локально.
            var realmConfig = _db.GetRealmConfiguration();
            using var realm = Realm.GetInstance(realmConfig);

            ThemeDto? resultingDto = null;

            realm.Write(() =>
            {
                var fileEntry = realm.Find<FileEntry>(cssHash);
                if (fileEntry == null)
                {
                    fileEntry = new FileEntry
                    {
                        Hash = cssHash,
                        Size = cssBytes.Length,
                        Extension = Path.GetExtension(manifest.File).TrimStart('.')
                    };
                }

                var newTheme = new Theme
                {
                    Id = manifest.Id,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Version = manifest.Version,
                    Base = manifest.Base,
                    File = fileEntry
                };
                
                var managedTheme = realm.Add(newTheme, update: true);
                
                _logger.LogInformation("Theme '{Name}' (ID: {Id}) has been imported and saved to the database.", managedTheme.Name, managedTheme.Id);
                
                resultingDto = new ThemeDto(
                    managedTheme.Id,
                    managedTheme.Name,
                    managedTheme.Base,
                    managedTheme.Author,
                    managedTheme.Version,
                    managedTheme.File == null ? null : new FileEntryDto(managedTheme.File.Hash, managedTheme.File.Size, managedTheme.File.Extension)
                );
            });
            
            return resultingDto!;
        }
    }
}
