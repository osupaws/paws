using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paws.Host.Data.Schemas; // Ensure this is using the Schema namespace

namespace Paws.Host.Services.Core
{
    public class PluginInstallerService
    {
        private readonly ILogger<PluginInstallerService> _logger;
        private readonly PawsDbService _dbService;
        private readonly FileStorageService _storage;

        public PluginInstallerService(ILogger<PluginInstallerService> logger, PawsDbService dbService, FileStorageService storage)
        {
            _logger = logger;
            _dbService = dbService;
            _storage = storage;
        }

        public async Task<PluginManifest> InstallPluginAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Plugin package not found.", zipPath);

            using var archive = ZipFile.OpenRead(zipPath);
            var manifestEntry = archive.GetEntry("plugin.json");
            if (manifestEntry == null) throw new InvalidDataException("plugin.json missing from package.");

            using var reader = new StreamReader(manifestEntry.Open());
            var manifestJson = await reader.ReadToEndAsync();
            var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            if (manifest == null) throw new InvalidDataException("Failed to parse plugin.json.");

            // 1. Prepare Plugin Entity
            var plugin = new Paws.Host.Data.Schemas.Plugin
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                EntryPoint = manifest.EntryPoint,
                Author = manifest.Author ?? string.Empty,
                Description = manifest.Description ?? string.Empty,
                UiEntry = manifest.Ui?.Entry ?? string.Empty,
                IsActive = true
            };

            // 2. Clear old version first (Transaction 1)
            _dbService.RunWrite(realm =>
            {
                var existing = realm.Find<Paws.Host.Data.Schemas.Plugin>(manifest.Id);
                if (existing != null)
                {
                    // Cascade delete files
                    var files = realm.All<Paws.Host.Data.Schemas.PluginFile>().Where(f => f.Plugin == existing);
                    realm.RemoveRange(files);
                    realm.Remove(existing);
                }
                realm.Add(plugin);
            });

            // 3. Process Files (Iterate zip entries)
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories if ZipFile returns them as entries (usually empty name)

                using var stream = entry.Open();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var data = ms.ToArray();

                var extension = Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(extension)) extension = "dat";

                // Determine content type or just use extension for storage service
                // Just use extension for now. FileStorageService expects extension for ContentType mapping later if needed.
                var hash = await _storage.StoreFileAsync(data, extension);

                // Add PluginFile record (Transaction 2 per file, or batch? Batch is better but simple for now)
                _dbService.RunWrite(realm =>
                {
                    // Re-fetch plugin in this transaction context
                    var p = realm.Find<Paws.Host.Data.Schemas.Plugin>(manifest.Id);
                    if (p != null)
                    {
                        var pluginFile = new Paws.Host.Data.Schemas.PluginFile
                        {
                            Id = $"{manifest.Id}|{entry.FullName}",
                            Plugin = p,
                            VirtualPath = entry.FullName,
                            BlobHash = hash
                        };
                        realm.Add(pluginFile, update: true);
                    }
                });
            }

            return manifest;
        }
    }
}
