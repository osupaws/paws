namespace Paws.Abstractions.Models;

/// <summary>
/// Manifest for Paws packages (themes and plugins).
/// </summary>
public class PackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "theme" or "plugin"
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = "unknown";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    
    // Entry point: "theme.css" for themes, "main.dll" for plugins
    public string Entry { get; set; } = string.Empty;
    
    // Optional for themes: "paws-dark" or "paws-light"
    public string? BaseThemeId { get; set; }
}
