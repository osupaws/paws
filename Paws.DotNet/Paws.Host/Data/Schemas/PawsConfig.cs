using Realms;

namespace Paws.Host.Data.Schemas
{
    public partial class PawsConfig : IRealmObject
    {
        // We will only have one config object, so we can use a constant ID.
        [PrimaryKey]
        [MapTo("id")]
        public int Id { get; set; } = 0;

        [MapTo("stablePath")]
        public string? StablePath { get; set; }

        [MapTo("lazerPath")]
        public string? LazerPath { get; set; }

        [MapTo("isLegacyMode")]
        public bool IsLegacyMode { get; set; } = false;
    }
}
