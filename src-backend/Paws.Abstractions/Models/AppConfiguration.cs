namespace Paws.Abstractions.Models;

public class AppConfiguration
{
    public bool IsLegacyMode { get; set; }
    public string StablePath { get; set; } = string.Empty;
    public string LazerPath { get; set; } = string.Empty;
    public string CurrentThemeId { get; set; } = string.Empty;
    public bool IsFirstLaunch { get; set; } = true;
    public bool IsSwitchOnLogoEnabled { get; set; } = true;
    public bool IsShowTips { get; set; } = true;
    public bool IsHideInTrayOnClose { get; set; } = true;
    public bool IsLaunchOnStartup { get; set; } = false;
    public bool IsInvisibleLaunch { get; set; } = false;
    public bool IsDeveloperModeEnabled { get; set; } = false;
    public string DevPluginPath { get; set; } = string.Empty;
}
