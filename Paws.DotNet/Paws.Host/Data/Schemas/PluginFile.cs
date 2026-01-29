using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class PluginFile : IRealmObject
    {
        [PrimaryKey]
        public string Id { get; set; } = ""; // Composite ID: "{PluginId}|{VirtualPath}"

        public Plugin? Plugin { get; set; }

        [Indexed]
        public string VirtualPath { get; set; } = ""; // e.g., "manifest.json", "bin/Plugin.dll"

        public string BlobHash { get; set; } = ""; // Reference to FileBlob.Hash
    }
}
