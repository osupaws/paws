using System;
using System.Collections.Generic;
using Paws.Core.Abstractions.Lazer;

namespace Paws.Core.Abstractions
{
    /// <summary>
    /// Provides an abstraction for interacting with the osu!lazer database.
    /// Implementation handles the complexity of dynamic Realm objects and transactions.
    /// </summary>
    public interface ILazerContext
    {
        /// <summary>
        /// Retrieves all beatmap sets as detached DTOs.
        /// This is safe to usage for LINQ and other operations without threading issues.
        /// </summary>
        List<LazerBeatmapSet> GetBeatmapSets();

        /// <summary>
        /// Deletes the specified beatmap sets by their ID.
        /// Handles the write transaction internally.
        /// </summary>
        void DeleteBeatmapSets(IEnumerable<Guid> ids);

        /// <summary>
        /// Deletes the individual beatmaps by their ID.
        /// Handles the write transaction internally.
        /// </summary>
        void DeleteBeatmaps(IEnumerable<Guid> ids);

        /// <summary>
        /// Updates an existing BeatmapSet in the database with values from the provided DTO.
        /// Primarily used for updating file links (Background replacement) or metadata.
        /// </summary>
        /// <param name="set">The modified LazerBeatmapSet DTO.</param>
        void UpdateBeatmapSet(LazerBeatmapSet set);
        /// <summary>
        /// Imports a file into Lazer's local storage.
        /// Handles SHA-256 hashing, path resolution, and RealmFile creation/retrieval.
        /// </summary>
        /// <param name="sourceFilePath">Absolute path to the source file.</param>
        /// <returns>The imported LazerFile object containing the Hash and other metadata.</returns>
        LazerFile ImportFile(string sourceFilePath);

        /// <summary>
        /// Retrieves a list of file hashes that are NOT referenced by ANY known Realm object (Beatmaps, Skins, Scores, etc.).
        /// This is safer than the Plugin guessing usages.
        /// </summary>
        List<string> GetSafeOrphanHashes();

        /// <summary>
        /// Parses the file (identified by hash) as a Storyboard (.osb) or Beatmap (.osu)
        /// and returns a list of all referenced asset file paths (images, sounds, videos).
        /// </summary>
        List<string> GetStoryboardAssetPaths(string fileHash);

        /// <summary>
        /// Deletes specific RealmFile records by their hash.
        /// Does NOT check for usages (Plugin responsibility to ensure safety).
        /// </summary>
        void DeleteFiles(IEnumerable<string> hashes);
    }
}
