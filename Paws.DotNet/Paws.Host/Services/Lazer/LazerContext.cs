using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Core.Abstractions.Interfaces.Contexts;
using Paws.Core.Abstractions.Models;
using Realms;
using System.IO;
using OsuParsers.Decoders;
using OsuParsers.Storyboards;
using OsuParsers.Storyboards.Interfaces;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Lazer
{
    public class LazerContext : ILazerContext
    {
        private readonly LazerDbService _dbService;
        private readonly ILogger _logger;

        public LazerContext(LazerDbService dbService, ILogger logger)
        {
            _dbService = dbService;
            _logger = logger;
        }

        public IEnumerable<LazerBeatmapSet> GetAllBeatmapSets()
        {
            using var realm = _dbService.GetSafeReadInstance();
            if (realm == null) return new List<LazerBeatmapSet>();

            var allSets = realm.DynamicApi.All(LazerSchema.BeatmapSet);
            var result = new List<LazerBeatmapSet>();

            foreach (dynamic dynamicSet in allSets)
            {
                try
                {
                    result.Add(MapToDto(dynamicSet));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to map BeatmapSet: {Message}", ex.Message);
                }
            }

            return result;
        }

        public LazerBeatmapSet? GetBeatmapSet(string id)
        {
            using var realm = _dbService.GetSafeReadInstance();
            if (realm == null) return null;

            if (!Guid.TryParse(id, out var guid)) return null;

            var obj = realm.DynamicApi.Find(LazerSchema.BeatmapSet, guid);
            return obj != null ? MapToDto(obj) : null;
        }

        public string? GetFilePath(string hash)
        {
            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath)) return null;

            string folder1 = hash.Substring(0, 1);
            string folder2 = hash.Substring(0, 2);
            string path = Path.Combine(lazerPath, "files", folder1, folder2, hash);

            return System.IO.File.Exists(path) ? path : null;
        }

        public byte[]? GetFileContent(string hash)
        {
            var path = GetFilePath(hash);
            return path != null ? System.IO.File.ReadAllBytes(path) : null;
        }

        public Task<string> ImportFile(string sourcePath, string fileName)
        {
            if (!System.IO.File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath))
                throw new InvalidOperationException("Lazer path is not configured.");

            // Calculate SHA-256 Hash
            string hash;
            using (var stream = System.IO.File.OpenRead(sourcePath))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(stream);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            string folder1 = hash.Substring(0, 1);
            string folder2 = hash.Substring(0, 2);
            string destFolder = Path.Combine(lazerPath, "files", folder1, folder2);
            string destPath = Path.Combine(destFolder, hash);

            if (!System.IO.File.Exists(destPath))
            {
                Directory.CreateDirectory(destFolder);
                System.IO.File.Copy(sourcePath, destPath);
            }

            // Register in Realm
            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                var existingFile = realm.DynamicApi.Find(LazerSchema.File, hash);
                if (existingFile == null)
                {
                    realm.DynamicApi.CreateObject(LazerSchema.File, hash);
                }
            });

            return Task.FromResult(hash);
        }

        public List<string> GetSafeOrphanHashes()
        {
            using var realm = _dbService.GetSafeReadInstance();
            if (realm == null) return new List<string>();

            var usedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var sets = realm.DynamicApi.All(LazerSchema.BeatmapSet);
                foreach (dynamic set in sets)
                {
                    if (set.Files != null)
                    {
                        foreach (dynamic fileUsage in set.Files)
                        {
                            try { usedHashes.Add((string)fileUsage.File.Hash); } catch { }
                        }
                    }
                }

                try
                {
                    var skins = realm.DynamicApi.All(LazerSchema.Skin);
                    foreach (dynamic skin in skins)
                    {
                        if (skin.Files != null)
                        {
                            foreach (dynamic fileUsage in skin.Files)
                            {
                                try { usedHashes.Add((string)fileUsage.File.Hash); } catch { }
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    var scores = realm.DynamicApi.All(LazerSchema.Score);
                    foreach (dynamic score in scores)
                    {
                        if (score.Files != null)
                        {
                            foreach (dynamic fileUsage in score.Files)
                            {
                                try { usedHashes.Add((string)fileUsage.File.Hash); } catch { }
                            }
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSafeOrphanHashes: Critical error calculating usages.");
                return new List<string>();
            }

            var allFiles = realm.DynamicApi.All(LazerSchema.File);
            var orphans = new List<string>();

            foreach (dynamic f in allFiles)
            {
                try
                {
                    string hash = f.Hash;
                    if (!usedHashes.Contains(hash)) orphans.Add(hash);
                }
                catch { }
            }

            return orphans;
        }

        public void DeleteBeatmaps(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                foreach (var id in ids)
                {
                    if (Guid.TryParse(id, out var guid))
                    {
                        var beatmap = realm.DynamicApi.Find(LazerSchema.Beatmap, guid);
                        if (beatmap != null) realm.Remove(beatmap);
                    }
                }
            });
        }

        public void DeleteBeatmapSets(IEnumerable<string> ids)
        {
            if (ids == null || !ids.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                foreach (var id in ids)
                {
                    if (Guid.TryParse(id, out var guid))
                    {
                        var set = realm.DynamicApi.Find(LazerSchema.BeatmapSet, guid);
                        if (set != null) realm.Remove(set);
                    }
                }
            });
        }

        public void UpdateBeatmapSet(LazerBeatmapSet set)
        {
            if (set == null) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            if (!Guid.TryParse(set.Id, out var guid)) return;

            realm.Write(() =>
            {
                dynamic? existingSet = realm.DynamicApi.Find(LazerSchema.BeatmapSet, guid);
                if (existingSet == null) return;

                // Sync basic properties
                try { existingSet.DeletePending = set.DeletePending; } catch { }
                try { existingSet.Protected = set.Protected; } catch { }

                // Sync files
                // 1. Identify files to remove (present in Realm but not in DTO)
                var filesToRemove = new List<dynamic>();
                foreach (dynamic existingFileUsage in existingSet.Files)
                {
                    bool found = false;
                    foreach (var dtoFile in set.Files)
                    {
                        if (string.Equals(existingFileUsage.Filename, dtoFile.Filename, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        filesToRemove.Add(existingFileUsage);
                    }
                }

                foreach (var fileUsage in filesToRemove)
                {
                    existingSet.Files.Remove(fileUsage);
                }

                // 2. Identify files to add or update (present in DTO)
                foreach (var dtoFile in set.Files)
                {
                    dynamic? existingFileUsage = null;
                    foreach (dynamic fu in existingSet.Files)
                    {
                        if (string.Equals(fu.Filename, dtoFile.Filename, StringComparison.OrdinalIgnoreCase))
                        {
                            existingFileUsage = fu;
                            break;
                        }
                    }

                    if (existingFileUsage != null)
                    {
                        // Update hash if changed (e.g. background replacement)
                        if (existingFileUsage.File.Hash != dtoFile.Hash)
                        {
                            dynamic? newFile = realm.DynamicApi.Find(LazerSchema.File, dtoFile.Hash);
                            if (newFile != null)
                            {
                                existingFileUsage.File = newFile;
                            }
                        }
                    }
                    else
                    {
                        // Add new file usage
                        dynamic? fileRecord = realm.DynamicApi.Find(LazerSchema.File, dtoFile.Hash);
                        if (fileRecord != null)
                        {
                            dynamic newUsage = realm.DynamicApi.CreateObject(LazerSchema.NamedFileUsage);
                            newUsage.Filename = dtoFile.Filename;
                            newUsage.File = fileRecord;
                            existingSet.Files.Add(newUsage);
                        }
                    }
                }
            });
        }

        public void DeleteFiles(List<string> hashes)
        {
            if (hashes == null || !hashes.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                foreach (var hash in hashes)
                {
                    var file = realm.DynamicApi.Find(LazerSchema.File, hash);
                    if (file != null) realm.Remove(file);
                }
            });
        }

        public List<string> GetStoryboardAssetPaths(string fileHash)
        {
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath)) return new List<string>();

            string folder1 = fileHash.Substring(0, 1);
            string folder2 = fileHash.Substring(0, 2);
            string filePath = Path.Combine(lazerPath, "files", folder1, folder2, fileHash);

            if (!System.IO.File.Exists(filePath)) return new List<string>();

            try
            {
                string firstLine = "";
                using (var reader = new StreamReader(filePath))
                {
                    firstLine = reader.ReadLine() ?? "";
                }

                if (firstLine.StartsWith("osu file format v"))
                {
                    var beatmap = OsuParsers.Decoders.BeatmapDecoder.Decode(filePath);
                    if (!string.IsNullOrEmpty(beatmap.GeneralSection.AudioFilename)) assets.Add(beatmap.GeneralSection.AudioFilename);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.BackgroundImage)) assets.Add(beatmap.EventsSection.BackgroundImage);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.Video)) assets.Add(beatmap.EventsSection.Video);

                    if (beatmap.EventsSection.Storyboard != null)
                        ExtractStoryboardAssets(beatmap.EventsSection.Storyboard, assets);

                    foreach (var obj in beatmap.HitObjects)
                    {
                        if (obj.Extras != null && !string.IsNullOrEmpty(obj.Extras.SampleFileName))
                            assets.Add(obj.Extras.SampleFileName);
                    }
                }
                else
                {
                    var sb = OsuParsers.Decoders.StoryboardDecoder.Decode(filePath);
                    ExtractStoryboardAssets(sb, assets);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetStoryboardAssetPaths: Failed to parse file {Hash}", fileHash);
            }

            return assets.ToList();
        }

        private void ExtractStoryboardAssets(OsuParsers.Storyboards.Storyboard sb, HashSet<string> assets)
        {
            if (sb == null) return;
            void ProcessLayer(List<OsuParsers.Storyboards.Interfaces.IStoryboardObject> layer)
            {
                if (layer == null) return;
                foreach (var obj in layer)
                {
                    if (!string.IsNullOrEmpty(obj.FilePath)) assets.Add(obj.FilePath);
                }
            }
            ProcessLayer(sb.BackgroundLayer);
            ProcessLayer(sb.FailLayer);
            ProcessLayer(sb.PassLayer);
            ProcessLayer(sb.ForegroundLayer);
            ProcessLayer(sb.OverlayLayer);
            ProcessLayer(sb.SamplesLayer);
        }

        private LazerBeatmapSet MapToDto(dynamic set)
        {
            var dto = new LazerBeatmapSet
            {
                Id = set.ID.ToString(),
                Hash = set.Hash,
                DeletePending = set.DeletePending,
                Protected = set.Protected,
                DateAdded = set.DateAdded,
                Artist = "Unknown",
                Title = "Unknown",
            };

            foreach (var map in set.Beatmaps)
            {
                var mapDto = new LazerBeatmap
                {
                    Id = map.ID.ToString(),
                    Difficulty = map.DifficultyName,
                    MD5Hash = map.MD5Hash,
                    StarRating = map.StarRating,
                    RulesetID = (int)map.Ruleset.OnlineID
                };

                if (map.Metadata != null)
                {
                    try
                    {
                        var metadata = map.Metadata; // Cache dynamic access

                        mapDto.Metadata = new LazerBeatmapMetadata
                        {
                            Title = metadata.Title,
                            TitleUnicode = metadata.TitleUnicode,
                            Artist = metadata.Artist,
                            ArtistUnicode = metadata.ArtistUnicode,
                            // AuthorString is computed in Lazer, in Realm it is Author.Username
                            AuthorString = ((dynamic)metadata.Author)?.Username ?? "Unknown",
                            Source = metadata.Source,
                            Tags = metadata.Tags,
                            BackgroundFile = metadata.BackgroundFile,
                            AudioFile = metadata.AudioFile
                        };

                        // Update set top-level metadata
                        if (dto.Artist == "Unknown" && !string.IsNullOrEmpty(metadata.Artist)) dto.Artist = metadata.Artist;
                        if (dto.Title == "Unknown" && !string.IsNullOrEmpty(metadata.Title)) dto.Title = metadata.Title;
                    }
                    catch (Exception)
                    {
                        // Suppress metadata mapping errors to avoid crashing the whole set
                    }
                }

                dto.Beatmaps.Add(mapDto);
            }

            foreach (var file in set.Files)
            {
                dto.Files.Add(new LazerFile { Filename = file.Filename, Hash = file.File.Hash });
            }

            return dto;
        }

        public void Dispose() { }
    }
}
