using OsuParsers.Decoders;
using OsuParsers.Serialization;

namespace Paws.Core.Abstractions;

/// <summary>
/// Provides access to osu!stable data and file parsing.
/// This context is stateless regarding the database instance; it provides methods to read/write it.
/// </summary>
public class StableContext
{
    // --- Database Operations ---

    public StableDatabase ReadOsuDatabase(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("osu!.db not found", path);
        var db = DatabaseDecoder.DecodeOsu(path);
        return new StableDatabase(db);
    }

    public void WriteOsuDatabase(StableDatabase db, string path)
    {
        // Safe serialization back to disk
        // We use the internal _db object
        db._db.Save(path);
    }

    // --- File Parsing ---

    public StableBeatmapContent ParseBeatmap(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Beatmap file not found", path);
        var map = BeatmapDecoder.Decode(path);
        return new StableBeatmapContent(map);
    }

    public StableStoryboard ParseStoryboard(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Storyboard file not found", path);
        var sb = StoryboardDecoder.Decode(path);
        return new StableStoryboard(sb);
    }

    // --- Helper Utilities ---

    /// <summary>
    /// Scans a beatmap folder and returns a set of filenames (relative, lowercased)
    /// that are referenced by any .osu or .osb file in that folder.
    /// This is used to identify "Safe Assets" that should not be deleted.
    /// </summary>
    public HashSet<string> GetUsedAssets(string songFolderPath)
    {
        var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(songFolderPath)) return usedFiles;

        var osuFiles = Directory.GetFiles(songFolderPath, "*.osu");
        foreach (var file in osuFiles)
        {
            try
            {
                var map = ParseBeatmap(file);

                if (!string.IsNullOrEmpty(map.AudioFilename)) usedFiles.Add(map.AudioFilename);
                if (!string.IsNullOrEmpty(map.BackgroundImage)) usedFiles.Add(map.BackgroundImage);
                if (!string.IsNullOrEmpty(map.Video)) usedFiles.Add(map.Video);

                // HitSounds
                foreach (var sample in map.GetHitSoundSamples())
                {
                    usedFiles.Add(sample);
                }

                // Storyboard defined inside .osu
                if (map.EventsStoryboard != null)
                {
                    foreach (var sbFile in map.EventsStoryboard.GetAllReferencedFiles())
                    {
                        usedFiles.Add(sbFile);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors for individual maps, try to proceed
            }
        }

        var osbFiles = Directory.GetFiles(songFolderPath, "*.osb");
        foreach (var file in osbFiles)
        {
            try
            {
                var sb = ParseStoryboard(file);
                foreach (var sbFile in sb.GetAllReferencedFiles())
                {
                    usedFiles.Add(sbFile);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        return usedFiles;
    }
}
