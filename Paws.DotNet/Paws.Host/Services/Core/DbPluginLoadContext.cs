using System.Reflection;
using System.Runtime.Loader;
using Paws.Host.Data.Schemas;
using Realms;
using System.IO;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Core
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
            if (IsSharedAssembly(assemblyName))
            {
                return null;
            }

            return _dbService.RunRead(realm =>
            {
                var plugin = realm.Find<Plugin>(_pluginId);
                if (plugin == null) return null;

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
                }

                return null;
            });
        }

        private bool IsSharedAssembly(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            return name == "Paws.Core.Abstractions" ||
                   name == "Realm" ||
                   name == "Realm.PlatformHelpers" ||
                   name == "Microsoft.AspNetCore.Http.Abstractions" ||
                   name == "Newtonsoft.Json";
        }
    }
}
