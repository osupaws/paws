using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Core.Abstractions.Interfaces.Contexts;
using Paws.Core.Abstractions.Models;
using Realms;
using System.IO;
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
                    result.Add(LazerBeatmapMapper.MapToDto(dynamicSet));
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
            return obj != null ? LazerBeatmapMapper.MapToDto(obj) : null;
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

                LazerBeatmapMapper.ApplyUpdate(existingSet, set, realm);
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
            var lazerPath = _dbService.GetLazerBasePath();
            if (string.IsNullOrEmpty(lazerPath)) return new List<string>();

            string? filePath = GetFilePath(fileHash);
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return new List<string>();

            try
            {
                return LazerStoryboardHelper.GetStoryboardAssetPaths(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetStoryboardAssetPaths: Failed to parse file {Hash}", fileHash);
                return new List<string>();
            }
        }

        public void Dispose() { }
    }
}
