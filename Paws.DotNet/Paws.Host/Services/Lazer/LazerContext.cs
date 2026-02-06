using System;
using System.Collections.Generic;
using System.Linq;
using Paws.Core.Abstractions;
using Paws.Core.Abstractions.Lazer;
using Realms;

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
                MD5Hash = map.MD5Hash,
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
    }
}
