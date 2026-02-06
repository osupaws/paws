using System;
using System.Collections.Generic;

namespace Paws.Core.Abstractions.Lazer
{
    /// <summary>
    /// Represents a BeatmapSet from osu!lazer (DTO).
    /// </summary>
    public class LazerBeatmapSet
    {
        public Guid ID { get; set; }
        public string? Hash { get; set; }
        public bool DeletePending { get; set; }
        public bool Protected { get; set; }
        public DateTimeOffset DateAdded { get; set; }

        public List<LazerBeatmap> Beatmaps { get; set; } = new List<LazerBeatmap>();
        public List<LazerNamedFile> Files { get; set; } = new List<LazerNamedFile>();

        public override string ToString() => $"[Set:{ID}] {Beatmaps.FirstOrDefault()?.Metadata?.ToString() ?? "Unknown"}";
    }

    /// <summary>
    /// Represents a Beatmap from osu!lazer (DTO).
    /// </summary>
    public class LazerBeatmap
    {
        public Guid ID { get; set; }
        public string? DifficultyName { get; set; }
        public double StarRating { get; set; }
        public int RulesetID { get; set; }
        public string? MD5Hash { get; set; }
        public bool Hidden { get; set; }

        public LazerBeatmapMetadata? Metadata { get; set; }

        public override string ToString() => $"{Metadata} [{DifficultyName}]";
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

        public override string ToString() => $"{Artist} - {Title}";
    }

    public class LazerNamedFile
    {
        public string? Filename { get; set; }
        public LazerFile? File { get; set; }
    }

    public class LazerFile
    {
        public string? Hash { get; set; }
    }
}
