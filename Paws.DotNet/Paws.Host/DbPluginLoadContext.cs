using System.Reflection;
using System.Runtime.Loader;
using Paws.Host.Data.Schemas;
using Realms;

namespace Paws.Host
{
    public class DbPluginLoadContext : AssemblyLoadContext
    {
        private readonly PawsDbService _dbService;
        private readonly FileStorageService _storage;
        private readonly ILogger _logger;
        private readonly string _pluginId;

        public DbPluginLoadContext(PawsDbService dbService, FileStorageService storage, ILogger logger, string pluginId)
            : base(name: $"PluginContext_{pluginId}", isCollectible: true)
        {
            _dbService = dbService;
            _storage = storage;
            _logger = logger;
            _pluginId = pluginId;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Realm instances are thread-confined.
            return _dbService.RunRead(realm =>
            {
                var plugin = realm.Find<Plugin>(_pluginId);
                if (plugin == null)
                {
                    _logger.LogError("[DbPluginLoadContext] Plugin {_pluginId} not found in DB.", _pluginId);
                    return null;
                }

                var dllName = $"{assemblyName.Name}.dll";

                // Debug: Fetch all files to memory and search.
                // Realm collections are lazy, so iterating is fine for small counts.
                var allFiles = plugin.Files.ToList();

                var fileRef = allFiles.FirstOrDefault(f => f.VirtualPath.EndsWith(dllName, StringComparison.OrdinalIgnoreCase));

                if (fileRef != null)
                {
                    _logger.LogInformation("[DbPluginLoadContext] Found DLL reference: {Path} (Hash: {Hash})", fileRef.VirtualPath, fileRef.BlobHash);

                    var fileDataTask = _storage.RetrieveFileAsync(fileRef.BlobHash);
                    var fileData = fileDataTask.GetAwaiter().GetResult();

                    if (fileData != null)
                    {
                        using var ms = new MemoryStream(fileData);
                        return LoadFromStream(ms);
                    }
                    else
                    {
                         _logger.LogError("[DbPluginLoadContext] ERROR: File content not found on disk for hash {Hash}", fileRef.BlobHash);
                    }
                }
                else
                {
                    _logger.LogWarning("[DbPluginLoadContext] DLL {DllName} not found in plugin files. Available: {Files}", dllName, string.Join(", ", allFiles.Select(f => f.VirtualPath)));
                }

                return null;
            });
        }
    }
}
