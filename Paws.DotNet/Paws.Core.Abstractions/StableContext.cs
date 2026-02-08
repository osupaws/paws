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

    private readonly string? _stableRootPath;

    public StableContext(string? stableRootPath = null)
    {
        _stableRootPath = stableRootPath;
    }

    // --- Sandbox Security ---

    private string ValidatePath(string path)
    {
        if (string.IsNullOrEmpty(_stableRootPath))
            throw new InvalidOperationException("osu!stable path is not configured.");

        string fullPath = Path.GetFullPath(path);
        string root = Path.GetFullPath(_stableRootPath);

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Access denied: Path '{path}' is outside the osu!stable directory.");
        }
        return fullPath;
    }

    private string GetValidatedSongsPath()
    {
        if (string.IsNullOrEmpty(_stableRootPath))
            throw new InvalidOperationException("osu!stable path is not configured.");

        string songs = Path.Combine(_stableRootPath, "Songs");
        // Ensure it exists or at least is valid path
        return songs;
    }

    // --- File Operations (Sandbox) ---

    public string GetSongsPath() => GetValidatedSongsPath();

    public void ExtractArchive(string sourceZip, string destinationFolderName)
    {
        // Source can be anywhere (e.g. plugin temp), but destination MUST be in Songs.
        if (!File.Exists(sourceZip)) throw new FileNotFoundException("Archive not found", sourceZip);

        var songs = GetValidatedSongsPath();
        var dest = Path.Combine(songs, destinationFolderName);

        // Security check for destination
        ValidatePath(dest);

        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        System.IO.Compression.ZipFile.ExtractToDirectory(sourceZip, dest);
    }

    public void MoveDirectory(string source, string dest)
    {
        // Both source and dest must be inside stable (or at least dest).
        // Usage: reorganization inside Songs.
        string vSource = ValidatePath(source);
        string vDest = ValidatePath(dest);

        if (Directory.Exists(vDest)) throw new IOException($"Destination already exists: {dest}");
        Directory.Move(vSource, vDest);
    }

    public void CreateSymlink(string source, string dest)
    {
        // Source: Where the REAL file is (can be external if plugin manages it? No, keep it inside for now or relax if needed)
        // Dest: The symlink to create (MUST be inside Stable)

        string vDest = ValidatePath(dest);

        // We accept source being outside if we trust the plugin, but for "Sandbox" strictness let's assume loose source but strict dest.
        if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Source not found", source);

        if (File.Exists(vDest) || Directory.Exists(vDest)) return; // Already exists

        File.CreateSymbolicLink(vDest, source);
    }

    // --- Database Operations ---

    public StableDatabase ReadOsuDatabase(string path)
    {
        // We verify path is safe if it's absolute, or resolve relative to root
        // But typical usage passes direct path. check it?
        // Let's enforce sandbox if root is set.
        if (_stableRootPath != null && Path.IsPathRooted(path))
            ValidatePath(path);

        if (!File.Exists(path)) throw new FileNotFoundException("osu!.db not found", path);
        var db = DatabaseDecoder.DecodeOsu(path);
        return new StableDatabase(db);
    }

    public void WriteOsuDatabase(StableDatabase db, string path)
    {
        if (_stableRootPath != null && Path.IsPathRooted(path))
            ValidatePath(path);

        // Safe serialization back to disk
        // We use the internal _db object
        db._db.Save(path);
    }

    // --- File Parsing ---

    public StableBeatmapContent ParseBeatmap(string path)
    {
        if (_stableRootPath != null && Path.IsPathRooted(path))
            ValidatePath(path);

        if (!File.Exists(path)) throw new FileNotFoundException("Beatmap file not found", path);
        var map = BeatmapDecoder.Decode(path);
        return new StableBeatmapContent(map);
    }

    public StableStoryboard ParseStoryboard(string path)
    {
        if (_stableRootPath != null && Path.IsPathRooted(path))
            ValidatePath(path);

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
        if (_stableRootPath != null) ValidatePath(songFolderPath);

        var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(songFolderPath)) return usedFiles;

        var osuFiles = Directory.GetFiles(songFolderPath, "*.osu");
        foreach (var file in osuFiles)
        {
            try
            {
                // Internal parsing calls (bypass double check or re-check is fine)
                // We use raw decoder here to avoid recursive validate overhead/confusion
                var map = new StableBeatmapContent(BeatmapDecoder.Decode(file));

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
                var sb = new StableStoryboard(StoryboardDecoder.Decode(file));
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
