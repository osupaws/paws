using System.Threading.Tasks;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

public interface IConfigService
{
    AppConfiguration Config { get; }
    Task<AppConfiguration> GetConfigAsync();
    Task UpdateConfigAsync(AppConfiguration config);

    // Для произвольных настроек
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
}
