using Paws.Core.Abstractions.Models;
using Realms;

namespace Paws.Host.Services.Lazer
{
    /// <summary>
    /// Section: Beatmaps (Maps & Songs)
    /// Handles mappings for: BeatmapSetInfo, BeatmapInfo, BeatmapMetadata
    /// </summary>
    public static class LazerBeatmapMapper
    {
        public static LazerBeatmapSet MapToDto(dynamic set)
        {
            var dto = new LazerBeatmapSet
            {
                Id = set.ID.ToString(),               // Realm: BeatmapSetInfo.ID
                Hash = set.Hash,                      // Realm: BeatmapSetInfo.Hash
                DeletePending = set.DeletePending,    // Realm: BeatmapSetInfo.DeletePending
                Protected = set.Protected,            // Realm: BeatmapSetInfo.Protected
                DateAdded = set.DateAdded,            // Realm: BeatmapSetInfo.DateAdded
                Artist = "Unknown",
                Title = "Unknown",
            };

            foreach (var map in set.Beatmaps)         // Realm: BeatmapSetInfo.Beatmaps
            {
                var mapDto = new LazerBeatmap
                {
                    Id = map.ID.ToString(),           // Realm: BeatmapInfo.ID
                    Difficulty = map.DifficultyName,  // Realm: BeatmapInfo.DifficultyName
                    MD5Hash = map.MD5Hash,            // Realm: BeatmapInfo.MD5Hash
                    StarRating = map.StarRating,      // Realm: BeatmapInfo.StarRating
                    RulesetID = (int)map.Ruleset.OnlineID // Realm: BeatmapInfo.Ruleset -> RulesetInfo.OnlineID
                };

                if (map.Metadata != null)             // Realm: BeatmapInfo.Metadata
                {
                    try
                    {
                        var metadata = map.Metadata;

                        mapDto.Metadata = new LazerBeatmapMetadata
                        {
                            Title = metadata.Title,                   // Realm: BeatmapMetadata.Title
                            TitleUnicode = metadata.TitleUnicode,     // Realm: BeatmapMetadata.TitleUnicode
                            Artist = metadata.Artist,                 // Realm: BeatmapMetadata.Artist
                            ArtistUnicode = metadata.ArtistUnicode,   // Realm: BeatmapMetadata.ArtistUnicode
                            AuthorString = ((dynamic)metadata.Author)?.Username ?? "Unknown", // Realm: BeatmapMetadata.Author -> RealmUser.Username
                            Source = metadata.Source,                 // Realm: BeatmapMetadata.Source
                            Tags = metadata.Tags,                     // Realm: BeatmapMetadata.Tags
                            BackgroundFile = metadata.BackgroundFile, // Realm: BeatmapMetadata.BackgroundFile
                            AudioFile = metadata.AudioFile            // Realm: BeatmapMetadata.AudioFile
                        };

                        if (dto.Artist == "Unknown" && !string.IsNullOrEmpty(metadata.Artist)) dto.Artist = metadata.Artist;
                        if (dto.Title == "Unknown" && !string.IsNullOrEmpty(metadata.Title)) dto.Title = metadata.Title;
                    }
                    catch (Exception)
                    {
                        // Suppress metadata mapping errors
                    }
                }

                dto.Beatmaps.Add(mapDto);
            }

            foreach (var file in set.Files)           // Realm: BeatmapSetInfo.Files
            {
                // Defer to File System mapper
                dto.Files.Add(LazerFileMapper.MapToDto(file));
            }

            return dto;
        }

        public static void ApplyUpdate(dynamic existingSet, LazerBeatmapSet set, Realm realm)
        {
            // Sync basic properties
            try { existingSet.DeletePending = set.DeletePending; } catch { } // Realm: BeatmapSetInfo.DeletePending
            try { existingSet.Protected = set.Protected; } catch { }        // Realm: BeatmapSetInfo.Protected

            // Sync files
            var filesToRemove = new List<dynamic>();
            foreach (dynamic existingFileUsage in existingSet.Files)        // Realm: BeatmapSetInfo.Files
            {
                bool found = false;
                foreach (var dtoFile in set.Files)
                {
                    if (string.Equals(existingFileUsage.Filename, dtoFile.Filename, StringComparison.OrdinalIgnoreCase)) // Realm: RealmNamedFileUsage.Filename
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
                existingSet.Files.Remove(fileUsage); // Realm: BeatmapSetInfo.Files.Remove
            }

            foreach (var dtoFile in set.Files)
            {
                dynamic? existingFileUsage = null;
                foreach (dynamic fu in existingSet.Files) // Realm: BeatmapSetInfo.Files
                {
                    if (string.Equals(fu.Filename, dtoFile.Filename, StringComparison.OrdinalIgnoreCase)) // Realm: RealmNamedFileUsage.Filename
                    {
                        existingFileUsage = fu;
                        break;
                    }
                }

                if (existingFileUsage != null)
                {
                    if (existingFileUsage.File.Hash != dtoFile.Hash) // Realm: RealmNamedFileUsage.File.Hash
                    {
                        dynamic? newFile = realm.DynamicApi.Find(LazerSchema.File, dtoFile.Hash);
                        if (newFile != null)
                        {
                            existingFileUsage.File = newFile; // Realm: RealmNamedFileUsage.File
                        }
                    }
                }
                else
                {
                    dynamic? fileRecord = realm.DynamicApi.Find(LazerSchema.File, dtoFile.Hash);
                    if (fileRecord != null)
                    {
                        dynamic newUsage = realm.DynamicApi.CreateObject(LazerSchema.NamedFileUsage);
                        newUsage.Filename = dtoFile.Filename; // Realm: RealmNamedFileUsage.Filename
                        newUsage.File = fileRecord;           // Realm: RealmNamedFileUsage.File
                        existingSet.Files.Add(newUsage);      // Realm: BeatmapSetInfo.Files.Add
                    }
                }
            }
        }
    }
}
