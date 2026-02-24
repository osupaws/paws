using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paws.Core.Abstractions.Models;
using Paws.Host.Data.Schemas;

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

            // --- SECURITY SCAN ---
            var dllEntry = archive.GetEntry(manifest.EntryPoint);
            if (dllEntry != null)
            {
                using var dllStream = dllEntry.Open();
                using var ms = new MemoryStream();
                await dllStream.CopyToAsync(ms);
                ms.Position = 0; // Reset position for PEReader

                var result = PluginSecurityScanner.Analyze(ms, manifest);
                if (!result.IsSafe)
                {
                    var violations = string.Join("; ", result.Violations);
                    _logger.LogWarning("Security Block: Plugin {Id} failed static analysis. Violations: {Violations}", manifest.Id, violations);
                    throw new System.Security.SecurityException($"Plugin failed security scan: {violations}");
                }
            }
            else
            {
                _logger.LogWarning("EntryPoint {EntryPoint} not found in the archive for plugin {Id}. Skipping security scan.", manifest.EntryPoint, manifest.Id);
            }

            // --- EXTRACT ICON ---
            if (!string.IsNullOrEmpty(manifest.Icon) && string.IsNullOrEmpty(manifest.IconData))
            {
                var iconEntry = archive.GetEntry(manifest.Icon);
                if (iconEntry != null)
                {
                    using var iconStream = iconEntry.Open();
                    using var iconReader = new StreamReader(iconStream);
                    manifest.IconData = await iconReader.ReadToEndAsync();
                }
            }

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
                IsActive = true,
                Icon = manifest.Icon,
                IconData = manifest.IconData
            };

            // Add permissions
            if (manifest.Permissions != null)
            {
                foreach (var perm in manifest.Permissions)
                {
                    plugin.Permissions.Add(perm);
                }
            }
            if (manifest.Provides != null)
            {
                foreach (var prov in manifest.Provides) plugin.Provides.Add(prov);
            }
            if (manifest.Consumes != null)
            {
                foreach (var cons in manifest.Consumes) plugin.Consumes.Add(cons);
            }

            // 2. Clear old version first
            _dbService.RunWrite(realm =>
            {
                var existing = realm.Find<Paws.Host.Data.Schemas.Plugin>(manifest.Id);
                if (existing != null)
                {
                    var files = realm.All<Paws.Host.Data.Schemas.PluginFile>().Where(f => f.Plugin == existing);
                    realm.RemoveRange<Paws.Host.Data.Schemas.PluginFile>(files);
                    realm.Remove(existing);
                }
                realm.Add(plugin);
            });

            // 3. Process Files
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.Name.EndsWith("/")) continue;

                using var stream = entry.Open();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var data = ms.ToArray();

                var extension = Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(extension)) extension = "dat";

                var hash = await _storage.StoreFileAsync(data, extension);

                _dbService.RunWrite(realm =>
                {
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
