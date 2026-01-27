using System.Reflection;
using System.Runtime.Loader;
using Paws.Host.Data.Schemas;
using Realms;

namespace Paws.Host
{
    public class DbPluginLoadContext : AssemblyLoadContext
    {
        private readonly PawsDbService _dbService;
        private readonly string _pluginId;

        public DbPluginLoadContext(PawsDbService dbService, string pluginId)
            : base(name: $"PluginContext_{pluginId}", isCollectible: true)
        {
            _dbService = dbService;
            _pluginId = pluginId;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Realm instances are thread-confined, so we must open a new instance for this thread.
            var config = _dbService.GetRealmConfiguration();
            using var realm = Realm.GetInstance(config);

            var plugin = realm.Find<Plugin>(_pluginId);
            if (plugin == null) return null;

            // Strategy: Look for a file named "{Name}.dll" within the plugin's file list.
            var dllName = $"{assemblyName.Name}.dll";

            // We use AsEnumerable() to perform the string check client-side if Realm doesn't support EndsWith in LINQ exactly as needed,
            // though Realm .NET usually supports Contains/EndsWith.
            // Safe approach: filtering on the database level is preferred for performance.
            var fileRef = plugin.Files
                .Filter($"VirtualPath ENDSWITH[c] '{dllName}'") // [c] = case insensitive
                .FirstOrDefault();

            if (fileRef != null)
            {
                var fileBlob = realm.Find<FileBlob>(fileRef.BlobHash);
                if (fileBlob != null)
                {
                    using var ms = new MemoryStream(fileBlob.Data);
                    return LoadFromStream(ms);
                }
            }

            // If we return null, the runtime will attempt to resolve using the Default context.
            return null;
        }
    }
}
