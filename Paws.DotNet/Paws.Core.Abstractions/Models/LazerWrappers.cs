using System;
using System.Collections.Generic;

namespace Paws.Core.Abstractions.Models
{
    public class LazerBeatmapSet
    {
        public string Id { get; set; } = string.Empty;
        public string? Hash { get; set; }
        public bool DeletePending { get; set; }
        public bool Protected { get; set; }
        public DateTimeOffset DateAdded { get; set; }
        public string Artist { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<LazerBeatmap> Beatmaps { get; set; } = new();
        public List<LazerFile> Files { get; set; } = new();
    }

    public class LazerBeatmap
    {
        public string Id { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public string MD5Hash { get; set; } = string.Empty;
        public double StarRating { get; set; }
        public int RulesetID { get; set; }
        public LazerBeatmapMetadata? Metadata { get; set; }
    }

    public class LazerBeatmapMetadata
    {
        public string? Title { get; set; }
        public string? TitleUnicode { get; set; }
        public string? Artist { get; set; }
        public string? ArtistUnicode { get; set; }
        public string? AuthorString { get; set; }
        public string? Source { get; set; }
        public string? Tags { get; set; }
        public string? BackgroundFile { get; set; }
        public string? AudioFile { get; set; }
    }

    public class LazerFile
    {
        public string Filename { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
