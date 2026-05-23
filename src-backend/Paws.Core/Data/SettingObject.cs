using Realms;

namespace Paws.Core.Data;

/// <summary>
/// Persistent database model for arbitrary key-value settings.
/// </summary>
public partial class SettingObject : IRealmObject
{
    [PrimaryKey]
    [MapTo("_id")]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
