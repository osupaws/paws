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

    public PluginInstallerService(ILogger<PluginInstallerService> logger, PawsDbService dbService)
    {
        _logger = logger;
        _dbService = dbService;
    }

    public async Task<PluginManifest> InstallPluginAsync(string filePath)
    {
        _logger.LogInformation("Starting installation of plugin from: {Path}", filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Plugin package not found", filePath);

        // We support both .zip (as .pawsplugin) and directories (for dev)
        // But the primary use case here is .pawsplugin (zip)

        using var tempDir = new TempDirectory();
        string sourceDir = tempDir.Path;

        if (Directory.Exists(filePath))
        {
            // If it's a directory, just use it directly (or copy if we want strict isolation, but let's assume dev usage)
            sourceDir = filePath;
        }
        else
        {
            // Assume Zip
            ZipFile.ExtractToDirectory(filePath, tempDir.Path);
        }

        // 1. Read Manifest
        var manifestPath = Path.Combine(sourceDir, "plugin.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("plugin.json not found in package root.");

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidOperationException("Invalid manifest: ID is missing.");

        // 2. Prepare for DB Transaction
        var config = _dbService.GetRealmConfiguration();
        using var realm = Realm.GetInstance(config);

        await realm.WriteAsync(() =>
        {
            // Remove existing plugin if exists (Update logic)
            var existing = realm.Find<Plugin>(manifest.Id);
            if (existing != null)
            {
                // Cascade delete files logic if needed, but Realm usually handles object deletion ok.
                // However, we manually created FileBlobs. We should check if we want to GC orphan blobs.
                // For now, let's just delete the Plugin and PluginFile records. FileBlobs might be shared (deduplication),
                // so we don't delete blobs implicitly unless we implement ref counting.
                // Simpler approach for now: Overwrite.

                // Explicitly remove associated PluginFiles to be clean
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
                IsActive = true
            };

            realm.Add(plugin);

            // Populate lists (must be done after adding to Realm or on a managed object if we could init them, but for unmanaged objects with getter-only IList they are null)
            // Actually, for unmanaged objects, we can't write to getter-only properties if they are null.
            // But since we just added it to Realm, 'plugin' is now a managed object proxy, so Permissions is a valid Realm collection.

            // Permissions, Provides, Consumes are IList<string> in Realm object, derived from schema.
            // We just add to them.
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

            // 3. Import Files
            var allFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(sourceDir, file).Replace("\\", "/");

                // Skip manifest itself if we don't need it at runtime, but keeping it is good practice.

                var bytes = File.ReadAllBytes(file);
                var hash = ComputeHash(bytes);

                // Dedup: Check if blob exists
                var blob = realm.Find<FileBlob>(hash);
                if (blob == null)
                {
                    blob = new FileBlob { Hash = hash, Data = bytes };
                    realm.Add(blob);
                }

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

        _logger.LogInformation("Plugin {Name} ({Id}) installed successfully.", manifest.Name, manifest.Id);
        return manifest;
    }

    private static string ComputeHash(byte[] data)
    {
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(data);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
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
