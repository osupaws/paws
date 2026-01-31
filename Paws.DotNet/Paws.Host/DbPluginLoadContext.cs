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
            // 1. Shared Assemblies: Always use the Host's loaded version (Default Context)
            // This prevents "Type X cannot be cast to Type X" errors and native handle crashes (Realm).
            if (IsSharedAssembly(assemblyName))
            {
                // Returning null forces the runtime to look in the Default context.
                return null;
            }

            // 2. Load from Database
            return _dbService.RunRead(realm =>
            {
                var plugin = realm.Find<Plugin>(_pluginId);
                if (plugin == null)
                {
                   // _logger.LogError("[DbPluginLoadContext] Plugin {_pluginId} not found in DB.", _pluginId);
                    return null;
                }

                var dllName = $"{assemblyName.Name}.dll";
                var allFiles = plugin.Files.ToList();
                var fileRef = allFiles.FirstOrDefault(f => f.VirtualPath.EndsWith(dllName, StringComparison.OrdinalIgnoreCase));

                if (fileRef != null)
                {
                    _logger.LogDebug("[DbPluginLoadContext] Loading {DllName} from DB hash {Hash}", dllName, fileRef.BlobHash);

                    var fileDataTask = _storage.RetrieveFileAsync(fileRef.BlobHash);
                    var fileData = fileDataTask.GetAwaiter().GetResult();

                    if (fileData != null)
                    {
                        using var ms = new MemoryStream(fileData);
                        return LoadFromStream(ms);
                    }
                    else
                    {
                         _logger.LogError("[DbPluginLoadContext] ERROR: File content missing for {DllName} ({Hash})", dllName, fileRef.BlobHash);
                    }
                }

                // If not found in DB, return null to let runtime resolve it (e.g. system assemblies)
                return null;
            });
        }

        private bool IsSharedAssembly(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            return name == "Paws.Core.Abstractions" ||
                   name == "Realm" ||
                   name == "Realm.PlatformHelpers" ||
                   name == "Microsoft.AspNetCore.Http.Abstractions" || // Common ASP.NET deps
                   name == "Newtonsoft.Json";
        }
    }
}
