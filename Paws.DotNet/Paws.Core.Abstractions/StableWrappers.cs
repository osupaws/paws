using OsuParsers.Beatmaps;
using OsuParsers.Database;
using OsuParsers.Database.Objects;
using OsuParsers.Enums;
using OsuParsers.Storyboards;
using OsuParsers.Storyboards.Interfaces;
using OsuParsers.Storyboards.Objects;

namespace Paws.Core.Abstractions;

// --- Database Wrappers ---

public class StableDatabase
{
    internal readonly OsuDatabase _db;
    public StableDatabase(OsuDatabase db) => _db = db;

    public string PlayerName => _db.PlayerName;
    public int OsuVersion => _db.OsuVersion;

    // Lazy projection to wrappers
    public IEnumerable<StableBeatmap> Beatmaps
        => _db.Beatmaps.Select(b => new StableBeatmap(b));

    // For write operations, we might need access to raw list to remove items
    public void RemoveBeatmap(StableBeatmap map)
    {
        _db.Beatmaps.Remove(map._obj);
    }
}

public class StableBeatmap
{
    internal readonly DbBeatmap _obj;
    public StableBeatmap(DbBeatmap obj) => _obj = obj;

    public string Artist => _obj.Artist;
    public string Title => _obj.Title;
    public string Difficulty => _obj.Difficulty;
    public string Creator => _obj.Creator;

    public Ruleset Ruleset => _obj.Ruleset;
    public string MD5Hash => _obj.MD5Hash;
    public string FolderName => _obj.FolderName;
    public string FileName => _obj.FileName;
    public string AudioFileName => _obj.AudioFileName;

    public int BeatmapId => _obj.BeatmapId;
    public int BeatmapSetId => _obj.BeatmapSetId;
}

public class StableScore
{
    internal readonly Score _obj;
    public StableScore(Score obj) => _obj = obj;

    public string PlayerName => _obj.PlayerName;
    public string BeatmapMD5Hash => _obj.BeatmapMD5Hash;
    public Ruleset Ruleset => _obj.Ruleset;
    public long ScoreId => _obj.ScoreId;
    // Add other fields as needed
}


// --- Content Parsing Wrappers (Decoded Files) ---

public class StableBeatmapContent
{
    internal readonly Beatmap _obj;
    public StableBeatmapContent(Beatmap obj) => _obj = obj;

    public string AudioFilename => _obj.GeneralSection.AudioFilename;
    public string BackgroundImage => _obj.EventsSection.BackgroundImage;
    public string Video => _obj.EventsSection.Video;

    public IEnumerable<string> GetHitSoundSamples()
    {
        // Custom sample filenames in HitObjects
        return _obj.HitObjects
            .Select(h => h.Extras.SampleFileName)
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct();
    }

    public StableStoryboard? EventsStoryboard
        => _obj.EventsSection.Storyboard != null ? new StableStoryboard(_obj.EventsSection.Storyboard) : null;
}

public class StableStoryboard
{
    internal readonly Storyboard _obj;
    public StableStoryboard(Storyboard obj) => _obj = obj;

    public IEnumerable<string> GetAllReferencedFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Extract(IStoryboardObject obj)
        {
            if (obj is StoryboardSprite sprite) files.Add(sprite.FilePath);
            if (obj is StoryboardAnimation anim) files.Add(anim.FilePath);
            // Samples are handled separately in layers usually, or as events
        }

        // Layers
        foreach (var layer in new[] { _obj.BackgroundLayer, _obj.FailLayer, _obj.PassLayer, _obj.ForegroundLayer, _obj.OverlayLayer })
        {
            foreach (var obj in layer) Extract(obj);
        }

        // Samples
        foreach (var sample in _obj.SamplesLayer)
        {
             files.Add(sample.FilePath);
        }

        return files;
    }
}
