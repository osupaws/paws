using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Paws.Host.Data.Schemas;
using Realms;

namespace Paws.Host;

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

    public async Task<PluginManifest> InstallPluginAsync(string filePath)
    {
        _logger.LogInformation("1. Starting installation of plugin from: {Path}", filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogError("File not found: {Path}", filePath);
            throw new FileNotFoundException("Plugin package not found", filePath);
        }

        using var tempDir = new TempDirectory();
        string sourceDir = tempDir.Path;

        if (Directory.Exists(filePath))
        {
            sourceDir = filePath;
            _logger.LogInformation("Installing from directory: {SourceDir}", sourceDir);
        }
        else
        {
            _logger.LogInformation("Extracting zip to: {TempDir}", tempDir.Path);
            try
            {
                ZipFile.ExtractToDirectory(filePath, tempDir.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract zip file: {Path}", filePath);
                throw;
            }
        }

        // 1. Read Manifest
        var manifestPath = Path.Combine(sourceDir, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            _logger.LogError("plugin.json not found at: {ManifestPath}", manifestPath);
            throw new InvalidOperationException("plugin.json not found in package root.");
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        _logger.LogInformation("Read plugin.json ({Length} bytes)", manifestJson.Length);

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize plugin.json.");
            throw;
        }

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
        {
            _logger.LogError("Manifest deserialization failed or ID is missing. JSON: {Json}", manifestJson);
            throw new InvalidOperationException("Invalid manifest: ID is missing.");
        }

        _logger.LogInformation("Parsed manifest for plugin: {Name} ({Id}) v{Version}", manifest.Name, manifest.Id, manifest.Version);

        // 2. Pre-process Files (IO Bound)
        // Store files and calculate hashes BEFORE opening the DB transaction.
        // This avoids holding the write transaction open during IO operations.
        var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} files in package.", allFiles.Length);

        var fileEntries = new List<(string RelativePath, string Hash)>();

        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(sourceDir, file).Replace("\\", "/");
            _logger.LogInformation("Processing file: {Path}", relativePath);

            var bytes = await File.ReadAllBytesAsync(file);

            // Store using FileStorageService (Centralized)
            var hash = await _storage.StoreFileAsync(bytes);

            fileEntries.Add((relativePath, hash));
        }
        _logger.LogInformation("All files stored on disk. Starting DB transaction to save metadata...");

        // 3. Database Transaction (CPU/Memory Bound - Short lived)
        var config = _dbService.GetRealmConfiguration();
        using var realm = Realm.GetInstance(config);

        await realm.WriteAsync(() =>
        {
            // Remove existing plugin if exists (Update logic)
            var existing = realm.Find<Plugin>(manifest.Id);
            if (existing != null)
            {
                _logger.LogInformation("Removing existing version of plugin {Id}...", manifest.Id);
                realm.RemoveRange(existing.Files);
                realm.Remove(existing);
            }

            // Create Plugin Record
            var plugin = new Plugin
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Description = manifest.Description ?? "",
                Version = manifest.Version,
                Author = manifest.Author ?? "",
                EntryPoint = manifest.EntryPoint,
                UiEntry = manifest.Ui?.Entry ?? "",
                Icon = manifest.Icon, // Map Icon Path
                IsActive = true
            };

            // Optimization: If Icon is SVG, try to read it now and store in IconData
            if (!string.IsNullOrEmpty(manifest.Icon) && manifest.Icon.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
               try
               {
                   var iconEntry = fileEntries.FirstOrDefault(f => f.RelativePath.Equals(manifest.Icon.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase));
                   if (iconEntry != default)
                   {
                        // We need to read the file again or cache bytes. Since we just wrote it, reading from disk is safest/easiest here without passing bytes around.
                        var iconFullPath = Path.Combine(sourceDir, manifest.Icon);
                        if (File.Exists(iconFullPath))
                        {
                            plugin.IconData = File.ReadAllText(iconFullPath); // Store raw SVG
                        }
                   }
               }
               catch (Exception ex) {
                   // Log but don't fail install
                    // _logger.LogWarning("Failed to extract SVG content for DB storage: {Ex}", ex.Message);
               }
            }

            realm.Add(plugin);

            if (manifest.Permissions != null)
            {
                foreach (var p in manifest.Permissions) plugin.Permissions.Add(p);
            }
            if (manifest.Provides != null)
            {
                foreach (var p in manifest.Provides) plugin.Provides.Add(p);
            }
            if (manifest.Consumes != null)
            {
                foreach (var p in manifest.Consumes) plugin.Consumes.Add(p);
            }

            // 4. Link Files
            foreach (var (relativePath, hash) in fileEntries)
            {
                var pluginFile = new PluginFile
                {
                    Id = $"{manifest.Id}|{relativePath}",
                    Plugin = plugin,
                    VirtualPath = relativePath,
                    BlobHash = hash
                };
                realm.Add(pluginFile);
            }
        });

        _logger.LogInformation("Plugin {Name} ({Id}) installed successfully. Metadata saved.", manifest.Name, manifest.Id);
        return manifest;
    }


    // Helper for RAII temp dir
    private class TempDirectory : IDisposable
    {
        public string Path { get; }
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }
}
