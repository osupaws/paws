using OsuParsers.Decoders;
using OsuParsers.Serialization;
using Paws.Core.Abstractions.Interfaces.Contexts;
using Paws.Core.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Paws.Host.Services.Stable
{
    public class StableContext : IStableContext
    {
        private readonly string? _stableRootPath;

        public StableContext(string? stableRootPath = null)
        {
            _stableRootPath = stableRootPath;
        }

        private string ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(_stableRootPath))
                throw new InvalidOperationException("osu!stable path is not configured.");

            string fullPath = Path.GetFullPath(path);
            string root = Path.GetFullPath(_stableRootPath);

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access denied: Path '{path}' is outside the osu!stable directory.");

            return fullPath;
        }

        public string GetSongsPath()
        {
            if (string.IsNullOrEmpty(_stableRootPath))
                throw new InvalidOperationException("osu!stable path is not configured.");
            return Path.Combine(_stableRootPath, "Songs");
        }

        public void ExtractArchive(string sourceZip, string destinationFolderName)
        {
            if (!File.Exists(sourceZip)) throw new FileNotFoundException("Archive not found", sourceZip);
            var songs = GetSongsPath();
            var dest = Path.Combine(songs, destinationFolderName);
            ValidatePath(dest);

            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(sourceZip, dest);
        }

        public void MoveDirectory(string source, string dest)
        {
            string vSource = ValidatePath(source);
            string vDest = ValidatePath(dest);
            if (Directory.Exists(vDest)) throw new IOException($"Destination already exists: {dest}");
            Directory.Move(vSource, vDest);
        }

        public void CreateSymlink(string source, string dest)
        {
            string vDest = ValidatePath(dest);
            if (!File.Exists(source) && !Directory.Exists(source)) throw new FileNotFoundException("Source not found", source);
            if (File.Exists(vDest) || Directory.Exists(vDest)) return;
            File.CreateSymbolicLink(vDest, source);
        }

        public StableDatabase ReadOsuDatabase(string path)
        {
            if (_stableRootPath != null && Path.IsPathRooted(path)) ValidatePath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("osu!.db not found", path);
            var db = DatabaseDecoder.DecodeOsu(path);
            return new StableDatabase(db);
        }

        public void WriteOsuDatabase(StableDatabase db, string path)
        {
            if (_stableRootPath != null && Path.IsPathRooted(path)) ValidatePath(path);
            db.Save(path);
        }

        public StableBeatmapContent ParseBeatmap(string path)
        {
            if (_stableRootPath != null && Path.IsPathRooted(path)) ValidatePath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Beatmap file not found", path);
            var map = BeatmapDecoder.Decode(path);
            return new StableBeatmapContent(map);
        }

        public StableStoryboard ParseStoryboard(string path)
        {
            if (_stableRootPath != null && Path.IsPathRooted(path)) ValidatePath(path);
            if (!File.Exists(path)) throw new FileNotFoundException("Storyboard file not found", path);
            var sb = StoryboardDecoder.Decode(path);
            return new StableStoryboard(sb);
        }

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
                    var map = new StableBeatmapContent(BeatmapDecoder.Decode(file));
                    if (!string.IsNullOrEmpty(map.AudioFilename)) usedFiles.Add(map.AudioFilename);
                    if (!string.IsNullOrEmpty(map.BackgroundImage)) usedFiles.Add(map.BackgroundImage);
                    if (!string.IsNullOrEmpty(map.Video)) usedFiles.Add(map.Video);

                    foreach (var sample in map.GetHitSoundSamples()) usedFiles.Add(sample);
                    if (map.EventsStoryboard != null)
                    {
                        foreach (var sbFile in map.EventsStoryboard.GetAllReferencedFiles()) usedFiles.Add(sbFile);
                    }
                }
                catch { }
            }

            var osbFiles = Directory.GetFiles(songFolderPath, "*.osb");
            foreach (var file in osbFiles)
            {
                try
                {
                    var sb = new StableStoryboard(StoryboardDecoder.Decode(file));
                    foreach (var sbFile in sb.GetAllReferencedFiles()) usedFiles.Add(sbFile);
                }
                catch { }
            }
            return usedFiles;
        }
    }
}
