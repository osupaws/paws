using System.Collections.Generic;
using Paws.Core.Abstractions.Models;

namespace Paws.Core.Abstractions.Interfaces.Contexts
{
    public interface IStableContext
    {
        string GetSongsPath();
        void ExtractArchive(string sourceZip, string destinationFolderName);
        void MoveDirectory(string source, string dest);
        void CreateSymlink(string source, string dest);

        StableDatabase ReadOsuDatabase(string path);
        void WriteOsuDatabase(StableDatabase db, string path);

        StableBeatmapContent ParseBeatmap(string path);
        StableStoryboard ParseStoryboard(string path);

        HashSet<string> GetUsedAssets(string songFolderPath);
    }
}
