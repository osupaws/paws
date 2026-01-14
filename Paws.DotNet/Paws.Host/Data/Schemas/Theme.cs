using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class Theme : IRealmObject
    {
        [PrimaryKey]
        [MapTo("id")]
        public string Id { get; set; } = "";

        [MapTo("name")]
        public string Name { get; set; } = "";
        
        [MapTo("author")]
        public string? Author { get; set; }

        [MapTo("version")]
        public string? Version { get; set; }

        [MapTo("base")]
        public string Base { get; set; } = "dark";

        // This creates the link to the FileEntry object representing the CSS file
        [MapTo("file")]
        public FileEntry? File { get; set; }
    }
}
