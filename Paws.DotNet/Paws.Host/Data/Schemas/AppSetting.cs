using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class AppSetting : IRealmObject
    {
        [PrimaryKey]
        [MapTo("key")]
        public string Key { get; set; } = string.Empty;

        [MapTo("value")]
        public string Value { get; set; } = string.Empty;

        [MapTo("type")]
        public string Type { get; set; } = "string"; // "string", "bool", "int", "json"
    }
}
