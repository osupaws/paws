using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Services;

namespace Paws.Abstractions.Plugins;

/// <summary>
/// Facade for plugin-to-kernel communication.
/// Plugins do not get direct references to core services for safety.
/// The Kernel injects implementations with pre-configured plugin context.
/// </summary>
public interface IHostApi
{
    /// <summary>
    /// Safe file access within the plugin sandbox or granted scopes.
    /// </summary>
    ISandboxedStorage Storage { get; }

    /// <summary>
    /// Game data (Lazer/Stable). Kernel automatically resolves the active client.
    /// </summary>
    IGameDataService GameData { get; }

    /// <summary>
    /// Game process monitoring (running status, installation path).
    /// </summary>
    IMonitoringService Monitor { get; }

    /// <summary>
    /// Virtual File System resolution (game:// protocol).
    /// </summary>
    IVfsService Vfs { get; }

    /// <summary>
    /// Invokes a public method on another plugin.
    /// </summary>
    Task<object?> InvokePluginAsync(string targetPluginId, string method, Dictionary<string, object>? args = null);
}

public interface ISandboxedStorage
{
    // --- Sandbox Operations ---
    Task<byte[]> ReadFileAsync(string relativePath);
    Task WriteFileAsync(string relativePath, byte[] data);
    string GetDataDirectory();
    string GetTempDirectory();

    // --- Atomic Operations (Subject to Scopes) ---
    bool FileExists(string path);
    void DeleteFile(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive = false);
    IEnumerable<string> ListFiles(string path, string searchPattern = "*");
    IEnumerable<string> ListDirectories(string path, string searchPattern = "*");

    /// <summary>
    /// Direct byte reading by absolute path (requires appropriate scopes).
    /// </summary>
    Task<byte[]> ReadAbsolutePathAsync(string absolutePath);
}
