using Realms;
using System.Linq;

namespace Paws.Host.Data.Schemas
{
    public partial class Plugin : IRealmObject
    {
        [PrimaryKey]
        public string Id { get; set; } = ""; // Unique identifier (e.g., package name com.example.plugin)

        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public string? Icon { get; set; } // Relative path to the icon file in the package

        // Minimum required version of the host application
        public string MinAppVersion { get; set; } = "1.0.0";

        public string EntryPoint { get; set; } = "";
        public string UiEntry { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public IList<string> Permissions { get; } = null!;
        public IList<string> Provides { get; } = null!;
        public IList<string> Consumes { get; } = null!;

        [Backlink(nameof(PluginFile.Plugin))]
        public IQueryable<PluginFile> Files { get; } = null!;
    }
}
