using Realms;

namespace Paws.Core.Data;

/// <summary>
/// Persistent database model for file blobs (assets).
/// </summary>
public partial class BlobObject : IRealmObject
{
    [PrimaryKey]
    [MapTo("_id")]
    public string Hash { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public long CreatedAt { get; set; }
}
