using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class FileBlob : IRealmObject
    {
        [PrimaryKey]
        public string Hash { get; set; } = "";

        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
