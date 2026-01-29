using Realms;

namespace Paws.Host.Data.Lazer
{
    // Minimal Viable Schema (MVS) Implementation
    // We map to the internal osu!lazer table names but use our own class names to avoid
    // strict coupling to their C# implementation details. We only define fields we use.

    [MapTo("BeatmapSet")]
    public partial class LazerBeatmapSet : IRealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public string Hash { get; set; } = string.Empty;

        public bool Protected { get; set; }

        public bool DeletePending { get; set; }

        // Relationships
        public IList<LazerBeatmap> Beatmaps { get; } = null!;
        public IList<LazerNamedFileUsage> Files { get; } = null!;
    }

    [MapTo("Beatmap")]
    public partial class LazerBeatmap : IRealmObject
    {
        [PrimaryKey]
        public Guid ID { get; set; }

        public string DifficultyName { get; set; } = string.Empty;

        [Indexed]
        public int OnlineID { get; set; } = -1;

        public double StarRating { get; set; } = -1;

        public string MD5Hash { get; set; } = string.Empty;

        // Relationships
        public LazerRuleset? Ruleset { get; set; }
        public LazerBeatmapSet? BeatmapSet { get; set; }
    }

    [MapTo("Ruleset")]
    public partial class LazerRuleset : IRealmObject
    {
        [PrimaryKey]
        public string ShortName { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int OnlineID { get; set; } = -1;

        public bool Available { get; set; }
    }

    // Represents the 'RealmNamedFileUsage' in osu!
    // Note: This is an embedded object in strict Realm terms often,
    // but in osu! it inherits RealmObject, so it's a standalone table linked via relationship.
    [MapTo("RealmNamedFileUsage")]
    public partial class LazerNamedFileUsage : IRealmObject
    {
        public LazerRealmFile? File { get; set; }

        public string Filename { get; set; } = string.Empty;
    }

    [MapTo("File")]
    public partial class LazerRealmFile : IRealmObject
    {
        [PrimaryKey]
        public string Hash { get; set; } = string.Empty;
    }
}
