using System;
using System.IO;
using System.Threading.Tasks;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Provides managed and isolated storage access for plugins.
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Gets the path to the persistent data directory for the current plugin.
        /// This directory is guaranteed to exist and be accessible without special permissions.
        /// </summary>
        string GetPluginDataPath();

        /// <summary>
        /// Gets a short-lived temporary directory for the current plugin.
        /// </summary>
        string GetPluginTempPath();

        /// <summary>
        /// Stores a stream as a managed asset and returns a unique AssetId.
        /// </summary>
        Task<string> StoreAssetAsync(Stream stream, string extension);

        /// <summary>
        /// Retrieves an asset stream by its ID.
        /// </summary>
        Stream GetAssetStream(string assetId);

        /// <summary>
        /// Checks if a file exists. Subject to permission checks.
        /// </summary>
        bool FileExists(string path);

        /// <summary>
        /// Opens a file stream. Subject to permission checks.
        /// </summary>
        Stream OpenFile(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read);

        /// <summary>
        /// Deletes a file. Subject to permission checks.
        /// </summary>
        void DeleteFile(string path);

        /// <summary>
        /// Checks if a directory exists. Subject to permission checks.
        /// </summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Deletes a directory. Subject to permission checks.
        /// </summary>
        void DeleteDirectory(string path, bool recursive = false);

        /// <summary>
        /// Gets the length of a file in bytes. Subject to permission checks.
        /// </summary>
        long GetFileLength(string path);

        /// <summary>
        /// Gets the last write time (UTC) of a file or directory. Subject to permission checks.
        /// </summary>
        DateTime GetLastWriteTimeUtc(string path);

        /// <summary>
        /// Returns the names of files (including their paths) in the specified directory.
        /// Subject to permission checks.
        /// </summary>
        string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns the names of subdirectories (including their paths) in the specified directory.
        /// Subject to permission checks.
        /// </summary>
        string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
        /// <summary>
        /// Stores a stream in the shared temporary storage and returns a handle.
        /// </summary>
        Task<string> StoreTempAsync(Stream stream);

        /// <summary>
        /// Opens a stream for a temporary file by its handle.
        /// </summary>
        Stream OpenTempStream(string handle);

        /// <summary>
        /// Atomically moves a temporary file to a target path within the plugin's data directory.
        /// </summary>
        void MoveTempToData(string handle, string targetPath);
    }
}
