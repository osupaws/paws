using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;

namespace Paws.Core.Rpc;

/// <summary>
/// Handles core system RPC commands (config, themes, plugin management).
/// </summary>
public class SystemHandler : IRpcHandler
{
    private readonly IConfigService _config;
    private readonly IThemeService _themes;
    private readonly IPluginManager _plugins;
    private readonly IDatabaseService _db;
    private readonly IPackageImportService _packageImport;
    private readonly IOsuAuthService _auth;

    public SystemHandler(IConfigService config, IThemeService themes, IPluginManager plugins, IDatabaseService db, IPackageImportService packageImport, IOsuAuthService auth)
    {
        _config = config;
        _themes = themes;
        _plugins = plugins;
        _db = db;
        _packageImport = packageImport;
        _auth = auth;
    }

    public bool CanHandle(string action) => 
        action.StartsWith("sys") || 
        action == "getConfig" || 
        action == "updateConfig" || 
        action == "setSetting" ||
        action == "getThemes" ||
        action == "getThemeCss" ||
        action == "saveTheme" ||
        action == "getLoadedPlugins" ||
        action == "getDiscoveredPlugins" ||
        action == "getDbMetadata" ||
        action == "importPackage" ||
        action == "loadDevPlugin" ||
        action == "initiateOsuLogin" ||
        action == "waitForOsuCallback" ||
        action == "getOsuAccessToken" ||
        action == "getOsuProfile" ||
        action == "logoutOsu" ||
        action == "handleOsuCallback";

    public async Task<object?> HandleAsync(string action, string callerId, Dictionary<string, JsonElement> parameters)
    {
        switch (action)
        {
            case "getConfig":
            case "sys:config:get":
                return await _config.GetConfigAsync();

            case "setSetting":
            case "sys:config:set":
                if (parameters.TryGetValue("key", out var keyEl) && parameters.TryGetValue("value", out var valEl))
                {
                    await _config.SetSettingAsync(keyEl.GetString() ?? "", valEl.GetString() ?? "");
                    return true;
                }
                throw new ArgumentException("Key or value missing");

            case "updateConfig":
            case "sys:config:update":
                if (parameters.TryGetValue("config", out var configEl))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var config = JsonSerializer.Deserialize<AppConfiguration>(configEl.GetRawText(), options);
                    if (config != null)
                    {
                        await _config.UpdateConfigAsync(config);
                        return true;
                    }
                }
                throw new ArgumentException("Invalid config parameter");

            case "getThemes":
            case "sys:themes:list":
                return await _themes.GetAllThemesAsync();

            case "getLoadedPlugins":
            case "sys:plugins:list":
                return _plugins.GetLoadedPlugins();

            case "getDbMetadata":
            case "sys:db:metadata":
                return new { 
                    _db.DatabasePath,
                    _db.DataDirectory,
                    _db.PluginsDirectory,
                    _db.TempDirectory
                };

            case "importPackage":
                if (parameters.TryGetValue("path", out var pathEl))
                {
                    var path = pathEl.GetString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        return await _packageImport.ImportPackageAsync(path);
                    }
                }
                throw new ArgumentException("Path parameter missing");

            case "initiateOsuLogin":
                return _auth.InitiateLogin();

            case "waitForOsuCallback":
                int timeout = 120;
                if (parameters.TryGetValue("timeout", out var timeoutEl))
                {
                    timeout = timeoutEl.GetInt32();
                }
                return await _auth.WaitForCallbackAsync(timeout);

            case "getOsuAccessToken":
                return await _auth.GetAccessTokenAsync();

            case "getOsuProfile":
                bool forceRefresh = false;
                if (parameters.TryGetValue("refresh", out var refreshEl) && refreshEl.ValueKind == JsonValueKind.True)
                {
                    forceRefresh = true;
                }
                return await _auth.GetProfileAsync(forceRefresh);

            case "logoutOsu":
                await _auth.LogoutAsync();
                return true;

            case "handleOsuCallback":
                if (parameters.TryGetValue("url", out var urlEl))
                {
                    return _auth.HandleCallback(urlEl.GetString() ?? "");
                }
                throw new ArgumentException("Missing 'url' parameter");

            default:
                // Handle remaining legacy commands...
                return null;
        }
    }

    public string? GetRequiredScope(string action)
    {
        if (action.Contains("config")) return "sys:config:read";
        if (action.Contains("themes")) return "sys:themes:read";
        if (action.Contains("plugins")) return "sys:plugins:read";
        return null;
    }

    public bool IsHostOnly(string action) => 
        action == "importPackage" || action == "loadDevPlugin";
}
