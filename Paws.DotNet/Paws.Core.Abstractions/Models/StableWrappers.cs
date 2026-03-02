// No OsuParsers usings here! Pure DTOs for plugins.

namespace Paws.Core.Abstractions.Models
{
    // --- Database Wrappers ---

    public class StableDatabase
    {
        public string PlayerName { get; set; } = string.Empty;
        public int OsuVersion { get; set; }
        public List<StableBeatmap> Beatmaps { get; set; } = new();
    }

    public class StableBeatmap
    {
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;

        public int Ruleset { get; set; }
        public string MD5Hash { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string AudioFileName { get; set; } = string.Empty;

        public int BeatmapId { get; set; }
        public int BeatmapSetId { get; set; }
    }

    public class StableScore
    {
        public string PlayerName { get; set; } = string.Empty;
        public string BeatmapMD5Hash { get; set; } = string.Empty;
        public int Ruleset { get; set; }
        public long ScoreId { get; set; }
    }

    // --- Content Parsing Wrappers (Decoded Files) ---

    public class StableBeatmapContent
    {
        public string AudioFilename { get; set; } = string.Empty;
        public string BackgroundImage { get; set; } = string.Empty;
        public string Video { get; set; } = string.Empty;
        public List<string> HitSoundSamples { get; set; } = new();
        public StableStoryboard? EventsStoryboard { get; set; }
    }

    public class StableStoryboard
    {
        public List<string> ReferencedFiles { get; set; } = new();
    }
}
