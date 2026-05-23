namespace Paws.Abstractions.Models;

/// <summary>
/// Represents a UI theme definition.
/// </summary>
public class Theme
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public string BaseThemeId { get; set; } = string.Empty;
    public string? BlobHash { get; set; }
    public string? Css { get; set; }
}
