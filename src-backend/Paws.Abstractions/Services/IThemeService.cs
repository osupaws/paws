using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

public interface IThemeService
{
    Task<Theme?> GetThemeAsync(string id);
    Task<IEnumerable<Theme>> GetAllThemesAsync();
    Task AddThemeAsync(Theme theme);
    Task DeleteThemeAsync(string id);
}
