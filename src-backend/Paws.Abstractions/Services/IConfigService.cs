using System.Threading.Tasks;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

/// <summary>
/// Service for managing application configuration and settings.
/// </summary>
public interface IConfigService
{
    AppConfiguration Config { get; }
    Task<AppConfiguration> GetConfigAsync();
    Task UpdateConfigAsync(AppConfiguration config);

    // Arbitrary key-value settings
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
}
