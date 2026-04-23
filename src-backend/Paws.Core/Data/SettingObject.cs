using Realms;

namespace Paws.Core.Data;

public partial class SettingObject : IRealmObject
{
    [PrimaryKey]
    [MapTo("_id")]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
