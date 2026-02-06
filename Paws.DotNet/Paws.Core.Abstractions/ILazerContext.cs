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

        // Future extensibility:
        // void UpdateBeatmapSet(LazerBeatmapSet set);
    }
}
