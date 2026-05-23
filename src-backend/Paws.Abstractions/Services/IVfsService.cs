using System.Threading.Tasks;

namespace Paws.Abstractions.Services;

/// <summary>
/// Virtual File System for Paws.
/// Resolves paws:// protocols to absolute physical paths.
/// </summary>
public interface IVfsService
{
    /// <summary>
    /// Resolves a paws:// URL to an absolute path on the physical file system.
    /// Example: paws://stable/songs -> C:\osu\Songs
    /// </summary>
    string ResolvePath(string pluginId, string vPath);

    /// <summary>
    /// Checks if a plugin has permission to access a virtual path.
    /// </summary>
    bool ValidateAccess(string pluginId, string vPath);
}
