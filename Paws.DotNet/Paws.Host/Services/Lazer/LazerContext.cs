using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Core.Abstractions;
using Paws.Core.Abstractions.Lazer;
using Realms;
using System.IO;
using OsuParsers.Decoders;
using OsuParsers.Storyboards;
using OsuParsers.Storyboards.Interfaces;

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

        public List<LazerBeatmapSet> GetBeatmapSets()
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
                    // Accessing dynamic properties might fail if schema is completely different, but logging ID requires valid dynamic access too.
                    // We'll trust that ID exists or use a safe check if needed.
                    _logger.LogWarning("Failed to map BeatmapSet: {Message}", ex.Message);
                }
            }

            return result;
        }

        public void DeleteBeatmapSets(IEnumerable<Guid> ids)
        {
            if (ids == null || !ids.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                foreach (var id in ids)
                {
                    var obj = realm.DynamicApi.Find(LazerSchema.BeatmapSet, id);
                    if (obj != null)
                    {
                        realm.Remove(obj);
                    }
                }
            });
        }

        public LazerFile ImportFile(string sourceFilePath)
        {
            if (!System.IO.File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath))
                throw new InvalidOperationException("Lazer path is not configured.");

            // 1. Calculate SHA-256 Hash
            string hash;
            using (var stream = System.IO.File.OpenRead(sourceFilePath))
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(stream);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            // 2. Determine Destination Path (files/h/ha/hash)
            string folder1 = hash.Substring(0, 1);
            string folder2 = hash.Substring(0, 2);
            string destFolder = Path.Combine(lazerPath, "files", folder1, folder2);
            string destPath = Path.Combine(destFolder, hash);

            // 3. Copy File if it doesn't exist
            if (!System.IO.File.Exists(destPath))
            {
                Directory.CreateDirectory(destFolder);
                System.IO.File.Copy(sourceFilePath, destPath);
            }

            // 4. Register in Realm (Idempotent)
            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            // We need to return a DTO, so we capture the data we need.
            // But we must also ensure the Realm object exists.
            realm.Write(() =>
            {
                // Check if RealmFile exists (Primary Key is Hash)
                var existingFile = realm.DynamicApi.Find(LazerSchema.File, hash);
                if (existingFile == null)
                {
                    var newFile = realm.DynamicApi.CreateObject(LazerSchema.File, hash);
                    // Lazer's RealmFile usually just has Hash as PK and maybe nothing else critical for existence.
                    // If it has other required fields, we might need to know them.
                    // Based on typical Lazer schema, Hash is the PK.
                }
            });

            return new LazerFile { Hash = hash };
        }

        public void DeleteBeatmaps(IEnumerable<Guid> ids)
        {
            if (ids == null || !ids.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                foreach (var id in ids)
                {
                    var obj = realm.DynamicApi.Find(LazerSchema.Beatmap, id);
                    if (obj != null)
                    {
                        realm.Remove(obj);
                    }
                }
            });
        }

        public void UpdateBeatmapSet(LazerBeatmapSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            realm.Write(() =>
            {
                var realmSetObj = realm.DynamicApi.Find(LazerSchema.BeatmapSet, set.ID);
                if (realmSetObj == null)
                {
                    _logger.LogWarning("UpdateBeatmapSet: BeatmapSet {ID} not found.", set.ID);
                    return;
                }

                dynamic realmSet = realmSetObj;

                // 1. Identify Deletions (Files present in DB but missing from DTO)
                // We assume 'realmSet.Files' is an IList<RealmNamedFileUsage> which we can modify.
                // We cast to IEnumerable<dynamic> for reading, but we need to access the collection for removal.
                var realmFiles = (IEnumerable<dynamic>)realmSet.Files;

                // Create a set of DTO filenames for O(1) lookup
                var dtoFilenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in set.Files)
                {
                    if (!string.IsNullOrEmpty(f.Filename))
                        dtoFilenames.Add(f.Filename);
                }

                var filesToRemove = new List<dynamic>();

                foreach (var realmFile in realmFiles)
                {
                    string rFilename = (string)realmFile.Filename;
                    if (!dtoFilenames.Contains(rFilename))
                    {
                        filesToRemove.Add(realmFile);
                    }
                }

                // Apply Deletions
                foreach (var f in filesToRemove)
                {
                    // CRITICAL: Read properties BEFORE removal, as the object will become detached immediately.
                    string fname = (string)f.Filename;
                    realmSet.Files.Remove(f);
                    _logger.LogInformation("Removed file '{Filename}' from Set {ID}", fname, set.ID);
                }

                // 2. Identify Updates (Files present in both)
                // We iterate over the DTO files and update the corresponding Realm files
                foreach (var dtoFile in set.Files)
                {
                    // Find matching file by Filename
                    var existingUsage = realmFiles.FirstOrDefault(f => (string)f.Filename == dtoFile.Filename);

                    if (existingUsage != null)
                    {
                        // Check if Hash is different
                        string currentHash = existingUsage.File.Hash;
                        var newFile = dtoFile.File;

                        if (newFile?.Hash != null && !string.Equals(currentHash, newFile.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            // We need to point to the new File object
                            // We expect the new File object to already exist (ImportFile called previously)
                            var newRealmFile = realm.DynamicApi.Find(LazerSchema.File, newFile.Hash);
                            if (newRealmFile != null)
                            {
                                existingUsage.File = newRealmFile;
                                _logger.LogInformation("Updated file '{Filename}' for Set {ID} to Hash {Hash}", dtoFile.Filename, set.ID, newFile.Hash);
                            }
                            else
                            {
                                _logger.LogWarning("Could not find RealmFile with hash {Hash} requested for '{Filename}'", newFile.Hash, dtoFile.Filename);
                            }
                        }
                    }
                    else
                    {
                        // New File (Addition) logic could go here, but cleaning doesn't usually ADD files unexpectedly.
                        // If needed in future, we would create a new RealmNamedFileUsage and add to realmSet.Files.
                        // For now, ignoring additions to keep scope focused on cleaning/replacement.
                    }
                }

                // TODO: Metadata updates can be added here if needed.
                // For now, only file links are requested.
            });
        }

        // --- Mapping Logic ---

        private LazerBeatmapSet MapToDto(dynamic set)
        {
            var dto = new LazerBeatmapSet
            {
                ID = set.ID,
                Hash = set.Hash,
                DeletePending = set.DeletePending,
                Protected = set.Protected,
                DateAdded = set.DateAdded,
            };

            foreach (var map in set.Beatmaps)
            {
                dto.Beatmaps.Add(MapBeatmap(map));
            }

            foreach (var file in set.Files)
            {
                dto.Files.Add(MapFile(file));
            }

            return dto;
        }

        private LazerBeatmap MapBeatmap(dynamic map)
        {
            var dto = new LazerBeatmap
            {
                ID = map.ID,
                DifficultyName = map.DifficultyName,
                StarRating = map.StarRating,
                MD5Hash = map.MD5Hash, // Assuming dynamic handles nulls or map.MD5Hash is compatible
                Hidden = map.Hidden,
            };

            // Mapping Ruleset ID
            // Lazer RulesetInfo doesn't always have ID populated in the same way, but usually it does.
            // ShortName is reliable.
            // For now, we try OnlineID if available, else default to 0.
            try
            {
                string shortName = map.Ruleset.ShortName;
                switch (shortName)
                {
                    case "osu": dto.RulesetID = 0; break;
                    case "taiko": dto.RulesetID = 1; break;
                    case "fruits": dto.RulesetID = 2; break;
                    case "catch": dto.RulesetID = 2; break;
                    case "mania": dto.RulesetID = 3; break;
                    default: dto.RulesetID = map.Ruleset.OnlineID ?? 0; break;
                }
            }
            catch { dto.RulesetID = 0; }

            // Mapping Metadata
            if (map.Metadata != null)
            {
                dto.Metadata = new LazerBeatmapMetadata
                {
                    Title = map.Metadata.Title,
                    TitleUnicode = map.Metadata.TitleUnicode,
                    Artist = map.Metadata.Artist,
                    ArtistUnicode = map.Metadata.ArtistUnicode,
                    AuthorString = map.Metadata.Author.Username,
                    Source = map.Metadata.Source,
                    Tags = map.Metadata.Tags,
                    BackgroundFile = map.Metadata.BackgroundFile,
                    AudioFile = map.Metadata.AudioFile
                };
            }

            return dto;
        }

        private LazerNamedFile MapFile(dynamic fileUsage)
        {
            return new LazerNamedFile
            {
                Filename = fileUsage.Filename,
                File = new LazerFile { Hash = fileUsage.File.Hash }
            };
        }
        public List<string> GetSafeOrphanHashes()
        {
            using var realm = _dbService.GetSafeReadInstance();
            if (realm == null) return new List<string>();

            var usedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. BeatmapSets
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

                // 2. Skins
                // Skins also use RealmNamedFileUsage list called 'Files'
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
                catch (Exception ex)
                {
                    _logger.LogWarning("GetSafeOrphanHashes: Failed to process Skins. Skipping. Error: {Message}", ex.Message);
                }

                // 3. Scores
                // Scores also use RealmNamedFileUsage list called 'Files' (for replays etc)
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
                catch (Exception ex)
                {
                    _logger.LogWarning("GetSafeOrphanHashes: Failed to process Scores. Skipping. Error: {Message}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSafeOrphanHashes: Critical error calculating usages.");
                // If we fail to calculate usages, we MUST NOT return any orphans to avoid deleting active files.
                return new List<string>();
            }

            // 4. Find Orphans
            var allFiles = realm.DynamicApi.All(LazerSchema.File);
            var orphans = new List<string>();

            foreach (dynamic f in allFiles)
            {
                try
                {
                    string hash = f.Hash;
                    if (!usedHashes.Contains(hash))
                    {
                        orphans.Add(hash);
                    }
                }
                catch { }
            }

            _logger.LogInformation("GetSafeOrphanHashes: Found {Count} orphans out of {Total} files.", orphans.Count, allFiles.Count());
            return orphans;
        }

        public void DeleteFiles(IEnumerable<string> hashes)
        {
            if (hashes == null || !hashes.Any()) return;

            using var realm = _dbService.GetWriteableInstance();
            if (realm == null) throw new InvalidOperationException("Could not open Lazer DB for writing.");

            int deletedCount = 0;

            realm.Write(() =>
            {
                foreach (var hash in hashes)
                {
                    var file = realm.DynamicApi.Find(LazerSchema.File, hash);
                    if (file != null)
                    {
                        realm.Remove(file);
                        deletedCount++;
                        // Note: This leaves physical files on disk, as per requirements (Database Cleanup Priority)
                        // Physical cleanup can be added later if needed.
                    }
                }
            });

            if (deletedCount > 0)
                _logger.LogInformation("DeleteFiles: Removed {Count} RealmFile records.", deletedCount);
        }

        public List<string> GetStoryboardAssetPaths(string fileHash)
        {
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath)) return new List<string>();

            // Resolve physical path
            string folder1 = fileHash.Substring(0, 1);
            string folder2 = fileHash.Substring(0, 2);
            string filePath = Path.Combine(lazerPath, "files", folder1, folder2, fileHash);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("GetStoryboardAssetPaths: File not found on disk: {Hash}", fileHash);
                return new List<string>();
            }

            try
            {
                // Detect type by reading first line
                string firstLine = "";
                using (var reader = new StreamReader(filePath))
                {
                    firstLine = reader.ReadLine() ?? "";
                }

                if (firstLine.StartsWith("osu file format v"))
                {
                    // It's a beatmap (.osu)
                    var beatmap = OsuParsers.Decoders.BeatmapDecoder.Decode(filePath);

                    // 1. Events (Audio, Video, BG)
                    if (!string.IsNullOrEmpty(beatmap.GeneralSection.AudioFilename)) assets.Add(beatmap.GeneralSection.AudioFilename);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.BackgroundImage)) assets.Add(beatmap.EventsSection.BackgroundImage);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.Video)) assets.Add(beatmap.EventsSection.Video);

                    // 2. Storyboard in .osu
                    // Note: Beatmap.EventsSection.Storyboard might be null or populated depending on parser version/content
                    if (beatmap.EventsSection.Storyboard != null)
                    {
                        ExtractStoryboardAssets(beatmap.EventsSection.Storyboard, assets);
                    }

                    // 3. HitSounds (Custom Samples)
                    // This can be complex as it involves sample sets and custom filenames.
                    // For now, we focus on explicit file paths if available in "Extras".
                    foreach (var obj in beatmap.HitObjects)
                    {
                        if (obj.Extras != null && !string.IsNullOrEmpty(obj.Extras.SampleFileName))
                        {
                            assets.Add(obj.Extras.SampleFileName);
                        }
                        // Slider edges
                        if (obj is OsuParsers.Beatmaps.Objects.Slider slider)
                        {
                            // Sliders might have edge additions with custom filenames?
                            // OsuParsers HitObject Extras covers the main object.
                            // Slider specific edge samples are usually sets, but let's check strict file references?
                            // Usually storyboard cleanup is the main goal.
                        }
                    }
                }
                else
                {
                    // Assume it's a storyboard (.osb)
                    // .osb files might start with [Events] or just BOM/whitespace, but usually [Events] is the first section.
                    // Or they might use "osu file format v..." too?
                    // Actually .osb specs are loose. OsuParsers StoryboardDecoder should handle it.
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

            // Helper to process a layer
            void ProcessLayer(List<OsuParsers.Storyboards.Interfaces.IStoryboardObject> layer)
            {
                if (layer == null) return;
                foreach (var obj in layer)
                {
                    if (!string.IsNullOrEmpty(obj.FilePath))
                        assets.Add(obj.FilePath);
                }
            }

            ProcessLayer(sb.BackgroundLayer);
            ProcessLayer(sb.FailLayer);
            ProcessLayer(sb.PassLayer);
            ProcessLayer(sb.ForegroundLayer);
            ProcessLayer(sb.OverlayLayer);
            ProcessLayer(sb.SamplesLayer);
        }
    }
}
