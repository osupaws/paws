using Realms;
using System.Collections.Generic;
using System.Linq;

namespace Paws.Core.Abstractions;

// --- Schema Constants ---
// These string constants represent the ACTUAL table/column names in the osu!lazer database file.
// If osu!lazer changes "MD5Hash" to "FileHash", we only need to update the string here.
public static class LazerSchema
{
    public const string BeatmapSet = "BeatmapSet";
    public const string Beatmap = "Beatmap";
    public const string Ruleset = "Ruleset";
    public const string File = "File";
    public const string NamedFileUsage = "RealmNamedFileUsage";
}

// --- Wrappers ---
// These classes provide a stable, strongly-typed API for plugins to consume.
// They wrap the internal 'dynamic' Realm object.

public class LazerBeatmapSet
{
    private readonly dynamic _obj;
    public LazerBeatmapSet(dynamic obj) => _obj = obj;

    public Guid ID => _obj.ID;
    public int OnlineID => _obj.OnlineID;
    public string Hash => _obj.Hash;
    public bool Protected => _obj.Protected;
    public bool DeletePending => _obj.DeletePending;

    // Relations
    public IEnumerable<LazerBeatmap> Beatmaps
        => ((IEnumerable<dynamic>)_obj.Beatmaps).Select(b => new LazerBeatmap(b));

    public IEnumerable<LazerNamedFileUsage> Files
        => ((IEnumerable<dynamic>)_obj.Files).Select(f => new LazerNamedFileUsage(f));
}

public class LazerBeatmap
{
    private readonly dynamic _obj;
    public LazerBeatmap(dynamic obj) => _obj = obj;

    public Guid ID => _obj.ID;
    public string DifficultyName => _obj.DifficultyName;
    public int OnlineID => _obj.OnlineID;
    public double StarRating => _obj.StarRating;
    public string MD5Hash => _obj.MD5Hash;

    // Relations
    public LazerRuleset? Ruleset => _obj.Ruleset != null ? new LazerRuleset(_obj.Ruleset) : null;
    public LazerBeatmapSet? BeatmapSet => _obj.BeatmapSet != null ? new LazerBeatmapSet(_obj.BeatmapSet) : null;
}

public class LazerRuleset
{
    private readonly dynamic _obj;
    public LazerRuleset(dynamic obj) => _obj = obj;

    public string ShortName => _obj.ShortName;
    public string Name => _obj.Name;
    public int OnlineID => _obj.OnlineID;
    public bool Available => _obj.Available;
}

public class LazerNamedFileUsage
{
    private readonly dynamic _obj;
    public LazerNamedFileUsage(dynamic obj) => _obj = obj;

    public string Filename => _obj.Filename;
    public LazerRealmFile? File => _obj.File != null ? new LazerRealmFile(_obj.File) : null;
}

public class LazerRealmFile
{
    private readonly dynamic _obj;
    public LazerRealmFile(dynamic obj) => _obj = obj;

    public string Hash => _obj.Hash;
}
