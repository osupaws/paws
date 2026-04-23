namespace Paws.Abstractions.Models;

public class PackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "theme" | "plugin"
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = "unknown";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    
    // Точка входа (theme.css для тем, main.dll для плагинов)
    public string Entry { get; set; } = string.Empty;
    
    // Опционально для тем: paws-dark или paws-light
    public string? BaseThemeId { get; set; }
}
