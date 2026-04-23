using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;
using Paws.Core.Data;
using Realms;

namespace Paws.Core.Services;

public class ConfigService : IConfigService
{
    private readonly IDatabaseService _db;
    private AppConfiguration? _cachedConfig;

    public ConfigService(IDatabaseService db)
    {
        _db = db;
    }

    public AppConfiguration Config => _cachedConfig ?? GetConfigAsync().GetAwaiter().GetResult();

    private Realm _realm => _db.GetRealm();

    public async Task<AppConfiguration> GetConfigAsync()
    {
        await Task.CompletedTask;
        _cachedConfig = new AppConfiguration
        {
            IsLegacyMode = GetSetting("core.modes.legacy") == "true",
            StablePath = GetSetting("core.paths.stable") ?? string.Empty,
            LazerPath = GetSetting("core.paths.lazer") ?? string.Empty,
            CurrentThemeId = GetSetting("core.ui.current_theme") ?? "paws-dark",
            IsFirstLaunch = GetSetting("core.flags.is_first_launch") != "false",
            IsSwitchOnLogoEnabled = GetSetting("core.ui.switch_on_logo") != "false",
            IsShowTips = GetSetting("core.ui.show_tips") != "false",
            IsHideInTrayOnClose = GetSetting("core.ui.hide_in_tray") != "false",
            IsLaunchOnStartup = GetSetting("core.os.launch_on_startup") == "true",
            IsInvisibleLaunch = GetSetting("core.os.invisible_launch") == "true",
            IsDeveloperModeEnabled = GetSetting("core.dev.is_enabled") == "true",
            DevPluginPath = GetSetting("core.dev.plugin_path") ?? string.Empty
        };
        return _cachedConfig;
    }

    public async Task UpdateConfigAsync(AppConfiguration config)
    {
        _cachedConfig = config;
        await _realm.WriteAsync(() =>
        {
            SetSettingInternal("core.modes.legacy", config.IsLegacyMode.ToString().ToLower());
            SetSettingInternal("core.paths.stable", config.StablePath);
            SetSettingInternal("core.paths.lazer", config.LazerPath);
            SetSettingInternal("core.ui.current_theme", config.CurrentThemeId);
            SetSettingInternal("core.flags.is_first_launch", config.IsFirstLaunch.ToString().ToLower());
            SetSettingInternal("core.ui.switch_on_logo", config.IsSwitchOnLogoEnabled.ToString().ToLower());
            SetSettingInternal("core.ui.show_tips", config.IsShowTips.ToString().ToLower());
            SetSettingInternal("core.ui.hide_in_tray", config.IsHideInTrayOnClose.ToString().ToLower());
            SetSettingInternal("core.os.launch_on_startup", config.IsLaunchOnStartup.ToString().ToLower());
            SetSettingInternal("core.os.invisible_launch", config.IsInvisibleLaunch.ToString().ToLower());
            SetSettingInternal("core.dev.is_enabled", config.IsDeveloperModeEnabled.ToString().ToLower());
            SetSettingInternal("core.dev.plugin_path", config.DevPluginPath);
        });
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await Task.CompletedTask;
        return GetSetting(key);
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await _realm.WriteAsync(() => SetSettingInternal(key, value));
    }

    private string? GetSetting(string key)
    {
        return _realm.Find<SettingObject>(key)?.Value;
    }

    private void SetSettingInternal(string key, string value)
    {
        _realm.Add(new SettingObject { Key = key, Value = value }, update: true);
    }
}
