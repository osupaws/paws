using System.Collections.Generic;

namespace Paws.Core.Abstractions.Models
{
    public class PluginManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string EntryPoint { get; set; } = string.Empty;
        public string? IconData { get; set; }
        public PluginUiInfo? Ui { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public List<string> Provides { get; set; } = new List<string>();
        public List<string> Consumes { get; set; } = new List<string>();
        public bool IsActive { get; set; } = true;
    }

    public class PluginUiInfo
    {
        public string Entry { get; set; } = string.Empty;
    }
}
