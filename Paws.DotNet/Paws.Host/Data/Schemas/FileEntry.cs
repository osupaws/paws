using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class FileEntry : IRealmObject
    {
        [PrimaryKey]
        [MapTo("hash")]
        public string Hash { get; set; } = "";

        [MapTo("size")]
        public int Size { get; set; }

        [MapTo("extension")]
        public string Extension { get; set; } = "";
    }
}
