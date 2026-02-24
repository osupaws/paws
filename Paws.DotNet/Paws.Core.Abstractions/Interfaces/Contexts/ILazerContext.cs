using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Core.Abstractions.Models;

namespace Paws.Core.Abstractions.Interfaces.Contexts
{
    public interface ILazerContext : IDisposable
    {
        // Beatmaps
        IEnumerable<LazerBeatmapSet> GetAllBeatmapSets();
        LazerBeatmapSet? GetBeatmapSet(string id);
        void DeleteBeatmaps(IEnumerable<string> ids);
        void DeleteBeatmapSets(IEnumerable<string> ids);
        void UpdateBeatmapSet(LazerBeatmapSet set);

        // Files
        string? GetFilePath(string hash);
        byte[]? GetFileContent(string hash);
        Task<string> ImportFile(string sourcePath, string fileName);

        // Cleanup
        List<string> GetSafeOrphanHashes();
        void DeleteFiles(List<string> hashes);

        // Assets
        List<string> GetStoryboardAssetPaths(string fileHash);
    }
}
