using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for physical file storage, blob management, and sandbox isolation.
/// </summary>
public interface IStorageService
{
    // --- Global Blobs (Assets, Themes) ---
    Task<string> SaveBlobAsync(byte[] data, string contentType);
    Task<byte[]?> GetBlobAsync(string hash);
    Task<string?> GetBlobPathAsync(string hash);
    void DeleteBlob(string hash);

    // --- Plugin Sandbox Operations ---
    string GetPluginDataDirectory(string pluginId);
    string GetPluginTempDirectory(string pluginId);
    Task<byte[]> ReadPluginFileAsync(string pluginId, string relativePath);
    Task WritePluginFileAsync(string pluginId, string relativePath, byte[] data);
    
    // --- Atomic Operations (Subject to Scopes) ---
    bool FileExists(string pluginId, string absolutePath);
    void DeleteFile(string pluginId, string absolutePath);
    bool DirectoryExists(string pluginId, string absolutePath);
    void DeleteDirectory(string pluginId, string absolutePath, bool recursive = false);
    IEnumerable<string> ListFiles(string pluginId, string absolutePath, string searchPattern = "*");
    IEnumerable<string> ListDirectories(string pluginId, string absolutePath, string searchPattern = "*");
    
    /// <summary>
    /// Validates if a plugin has permission to access an absolute path.
    /// </summary>
    bool ValidateAccess(string pluginId, string absolutePath, bool isWriteAccess = false);
}
