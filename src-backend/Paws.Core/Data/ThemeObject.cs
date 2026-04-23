using Realms;

namespace Paws.Core.Data;

public partial class ThemeObject : IRealmObject
{
    [PrimaryKey]
    [MapTo("_id")]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public string BaseThemeId { get; set; } = string.Empty;
    public string? BlobHash { get; set; }
}
