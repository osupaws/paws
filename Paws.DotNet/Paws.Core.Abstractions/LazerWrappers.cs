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
    internal readonly dynamic _obj;
    public LazerBeatmapSet(dynamic obj) => _obj = obj;

    public Guid ID => _obj.ID;
    public int OnlineID => _obj.OnlineID;
    public string Hash => _obj.Hash;
    public DateTimeOffset DateAdded => _obj.DateAdded;

    // Allow Write Access
    public bool Protected
    {
        get => _obj.Protected;
        set => _obj.Protected = value;
    }

    public bool DeletePending
    {
        get => _obj.DeletePending;
        set => _obj.DeletePending = value;
    }

    // Relations
    public IEnumerable<LazerBeatmap> Beatmaps
        => ((IEnumerable<dynamic>)_obj.Beatmaps).Select(b => new LazerBeatmap(b));

    public IEnumerable<LazerNamedFileUsage> Files
        => ((IEnumerable<dynamic>)_obj.Files).Select(f => new LazerNamedFileUsage(f));

    // Modification Methods (Write Access)
    public void RemoveFile(LazerNamedFileUsage file)
    {
        _obj.Files.Remove(file._obj);
    }

    public void AddFile(LazerNamedFileUsage file)
    {
        _obj.Files.Add(file._obj);
    }

    public void RemoveBeatmap(LazerBeatmap beatmap)
    {
        _obj.Beatmaps.Remove(beatmap._obj);
    }
}

public class LazerBeatmap
{
    internal readonly dynamic _obj;
    public LazerBeatmap(dynamic obj) => _obj = obj;

    public Guid ID => _obj.ID;
    public string DifficultyName => _obj.DifficultyName;
    public int OnlineID => _obj.OnlineID;
    public double StarRating => _obj.StarRating;
    public string MD5Hash => _obj.MD5Hash;

    public bool DeletePending
    {
        get => _obj.DeletePending;
        set => _obj.DeletePending = value;
    }

    // Relations
    public LazerRuleset? Ruleset => _obj.Ruleset != null ? new LazerRuleset(_obj.Ruleset) : null;
    public LazerBeatmapSet? BeatmapSet => _obj.BeatmapSet != null ? new LazerBeatmapSet(_obj.BeatmapSet) : null;
}

public class LazerRuleset
{
    internal readonly dynamic _obj;
    public LazerRuleset(dynamic obj) => _obj = obj;

    public string ShortName => _obj.ShortName;
    public string Name => _obj.Name;
    public int OnlineID => _obj.OnlineID;
    public bool Available => _obj.Available;
}

public class LazerNamedFileUsage
{
    internal readonly dynamic _obj;
    public LazerNamedFileUsage(dynamic obj) => _obj = obj;

    public string Filename
    {
        get => _obj.Filename;
        set => _obj.Filename = value; // Allow renaming if needed
    }
    public LazerRealmFile? File => _obj.File != null ? new LazerRealmFile(_obj.File) : null;
}

public class LazerRealmFile
{
    internal readonly dynamic _obj;
    public LazerRealmFile(dynamic obj) => _obj = obj;

    public string Hash => _obj.Hash;
}
