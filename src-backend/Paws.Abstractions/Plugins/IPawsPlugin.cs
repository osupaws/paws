using System.Threading.Tasks;

namespace Paws.Abstractions.Plugins;

/// <summary>
/// Core interface for Paws plugins.
/// </summary>
public interface IPawsPlugin
{
    // Plugin initialization (called by Kernel on assembly load)
    Task InitializeAsync(IHostApi api);
    
    // Soft shutdown (called when plugin or kernel is stopping)
    Task ShutdownAsync();
}
