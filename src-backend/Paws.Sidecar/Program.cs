using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;
using Paws.Core.Services;
using Paws.Core.Storage;
using Paws.Core.Monitoring;
using Realms;

namespace Paws.Sidecar;

class Program
{
    private static IServiceProvider _serviceProvider = null!;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static async Task Main(string[] args)
    {
        // КРИТИЧЕСКИ ВАЖНО для Tauri: Принудительный сброс буфера STDOUT.
        // Иначе консольные логи (находящиеся в pipe) скапливаются и не идут в парсер Rust, пока буфер не забьется (4КБ)
        var stdoutWriter = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdoutWriter);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        try
        {
            Console.WriteLine($"[Sidecar] Starting... CWD is {Environment.CurrentDirectory}");
            // 1. Настройка DI
            var services = new ServiceCollection();

            services.AddSingleton<IDatabaseService, DatabaseService>();
            services.AddSingleton<IScopeManager, ScopeManager>();
            services.AddSingleton<IStorageService, StorageService>();
            services.AddSingleton<IMonitoringService, MonitoringService>();
            services.AddSingleton<IGameDataService, GameDataService>();
            services.AddSingleton<IPluginManager, Paws.Core.Plugins.PluginManager>();

            services.AddSingleton<IConfigService, ConfigService>();
            services.AddScoped<IThemeService, ThemeService>();
            services.AddScoped<IPackageImportService, PackageImportService>();
            _serviceProvider = services.BuildServiceProvider();

            // Инициализация загрузки плагинов
            var pluginManager = _serviceProvider.GetRequiredService<IPluginManager>();
            await pluginManager.LoadPluginsAsync();

            // Авто-восстановление Developer Mode после перезапуска Ядра
            using (var initScope = _serviceProvider.CreateScope())
            {
                var configSvc = initScope.ServiceProvider.GetRequiredService<IConfigService>();
                var initConfig = await configSvc.GetConfigAsync();
                if (initConfig.IsDeveloperModeEnabled && !string.IsNullOrWhiteSpace(initConfig.DevPluginPath))
                {
                    Console.WriteLine($"[Sidecar] Restoring DevPlugin from configuration...");
                    await pluginManager.LoadDevPluginAsync(initConfig.DevPluginPath);
                }
            }

            Console.WriteLine($"[Sidecar] DI Built in {Environment.CurrentDirectory}. Waiting for commands...");

            // 2. Цикл обработки команд (stdin)
            while (true)
            {
                var input = await Console.In.ReadLineAsync();
                if (input == null)
                {
                    Console.WriteLine("[Sidecar] STDIN is closed by the parent, exiting loop.");
                    break;
                }
                if (string.IsNullOrWhiteSpace(input)) continue;

                try
                {
                    Console.WriteLine($"[Sidecar] Input: {input}");
                    
                    var command = JsonSerializer.Deserialize<SidecarCommand>(input, _jsonOptions);
                    if (command == null) continue;

                    var result = await HandleCommand(command);
                    Console.WriteLine(JsonSerializer.Serialize(result, _jsonOptions));
                }
                catch (Exception ex)
                {
                    var error = new SidecarResponse { Success = false, Error = ex.Message };
                    Console.WriteLine(JsonSerializer.Serialize(error, _jsonOptions));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sidecar] FATAL ERROR IN MAIN: {ex}");
            Console.Error.WriteLine($"[Sidecar] FATAL ERROR IN MAIN: {ex}");
            File.WriteAllText("paws-crash.log", $"[{DateTime.Now}] MAIN EXCEPTION:\n{ex}");
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        File.WriteAllText("paws-crash-unhandled.log", $"[{DateTime.Now}] UNHANDLED EXCEPTION:\n{e.ExceptionObject}");
    }

    private static async Task<SidecarResponse> HandleCommand(SidecarCommand command)
    {
        using var scope = _serviceProvider.CreateScope();
        var themeService = scope.ServiceProvider.GetRequiredService<IThemeService>();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigService>();
        var pluginManager = scope.ServiceProvider.GetRequiredService<IPluginManager>();

        // 1. ПРОВЕРКА ПРАВ (Scope Validation)
        if (command.CallerId != "host")
        {
            var manifest = pluginManager.GetManifest(command.CallerId);
            if (manifest == null)
                return new SidecarResponse { Success = false, Error = $"Access Denied: Unknown plugin '{command.CallerId}'" };

            if (manifest.IsSystem)
            {
                // Системные плагины имеют полный доступ
            }
            else
            {
                var requiredScope = GetRequiredScope(command.Action);
                if (requiredScope != null && !manifest.Scopes.Contains(requiredScope))
                {
                    return new SidecarResponse { Success = false, Error = $"Access Denied: Plugin '{command.CallerId}' missing required scope '{requiredScope}'" };
                }
                
                // Хардкорная блокировка опасных действий для не-системных плагинов
                if (IsActionHostOnly(command.Action))
                {
                    return new SidecarResponse { Success = false, Error = $"Access Denied: Action '{command.Action}' is restricted to host only" };
                }
            }
        }

        switch (command.Action)
        {
            case "ping":
                return new SidecarResponse { Data = "pong" };

            // --- Configuration ---
            case "getConfig":
                var config = await configService.GetConfigAsync();
                return new SidecarResponse { Data = config };

            case "updateConfig":
                if (command.Params.TryGetValue("config", out var configElement))
                {
                    var updatedConfig = JsonSerializer.Deserialize<AppConfiguration>(configElement.GetRawText(), _jsonOptions);
                    if (updatedConfig != null)
                    {
                        await configService.UpdateConfigAsync(updatedConfig);
                        return new SidecarResponse { Success = true };
                    }
                }
                return new SidecarResponse { Success = false, Error = "Invalid configuration data" };

            case "setSetting":
                if (command.Params.TryGetValue("key", out var keyEl) && command.Params.TryGetValue("value", out var valEl))
                {
                    await configService.SetSettingAsync(keyEl.GetString() ?? "", valEl.GetString() ?? "");
                    return new SidecarResponse { Success = true };
                }
                return new SidecarResponse { Success = false, Error = "Key or value missing" };

            // --- Themes ---
            case "getThemes":
                var themes = await themeService.GetAllThemesAsync();
                return new SidecarResponse { Data = themes };

            case "getThemeCss":
                if (command.Params.TryGetValue("id", out var idElement))
                {
                    var theme = await themeService.GetThemeAsync(idElement.GetString() ?? "");
                    return new SidecarResponse { Data = theme?.Css };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'id' parameter" };

            case "saveTheme":
                if (command.Params.TryGetValue("theme", out var themeElement))
                {
                    var theme = JsonSerializer.Deserialize<Theme>(themeElement.GetRawText(), _jsonOptions);
                    if (theme != null)
                    {
                        await themeService.AddThemeAsync(theme);
                        return new SidecarResponse { Success = true };
                    }
                }
                return new SidecarResponse { Success = false, Error = "Invalid theme data" };

            // --- Packages ---
            case "importPackage":
                if (command.Params.TryGetValue("path", out var pathElement))
                {
                    var importService = scope.ServiceProvider.GetRequiredService<IPackageImportService>();
                    var success = await importService.ImportPackageAsync(pathElement.GetString() ?? "");
                    return new SidecarResponse { Success = success, Error = success ? null : "Failed to import package" };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'path' parameter" };

            // --- Plugins ---
            case "getLoadedPlugins":
                {
                    var pm = scope.ServiceProvider.GetRequiredService<IPluginManager>();
                    var loaded = pm.GetLoadedPlugins();
                    return new SidecarResponse { Data = loaded };
                }

            case "getDiscoveredPlugins":
                {
                    // Сканируем папку Plugins на наличие папок с plugin.json, даже если они не загружены
                    var dataService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                    var pluginsDir = dataService.PluginsDirectory;
                    var discovered = new List<PluginManifest>();

                    if (Directory.Exists(pluginsDir))
                    {
                        foreach (var dir in Directory.GetDirectories(pluginsDir))
                        {
                            var manifestPath = Path.Combine(dir, "plugin.json");
                            if (File.Exists(manifestPath))
                            {
                                try
                                {
                                    var json = File.ReadAllText(manifestPath);
                                    var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                                    if (manifest != null) discovered.Add(manifest);
                                }
                                catch { /* Ignore malformed manifests */ }
                            }
                        }
                    }
                    return new SidecarResponse { Data = discovered };
                }

            case "loadDevPlugin":
                if (command.Params.TryGetValue("path", out var devPathEl))
                {
                    var pm = scope.ServiceProvider.GetRequiredService<IPluginManager>();
                    await pm.LoadDevPluginAsync(devPathEl.GetString() ?? "");
                    return new SidecarResponse { Success = true };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'path' parameter" };

            // --- Game Data ---
            case "getBeatmapSets":
                {
                    var gameData = scope.ServiceProvider.GetRequiredService<IGameDataService>();
                    var sets = await gameData.GetAllBeatmapSetsAsync();
                    return new SidecarResponse { Data = sets };
                }

            case "searchBeatmaps":
                if (command.Params.TryGetValue("query", out var queryEl))
                {
                    var gameData = scope.ServiceProvider.GetRequiredService<IGameDataService>();
                    var results = await gameData.SearchBeatmapsAsync(queryEl.GetString() ?? "");
                    return new SidecarResponse { Data = results };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'query' parameter" };

            // --- Storage ---
            case "fsReadPluginFile":
                if (command.Params.TryGetValue("path", out var readPathEl))
                {
                    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                    var bytes = await storage.ReadPluginFileAsync(command.CallerId, readPathEl.GetString() ?? "");
                    return new SidecarResponse { Data = Convert.ToBase64String(bytes) };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'path' parameter" };

            case "fsWritePluginFile":
                if (command.Params.TryGetValue("path", out var writePathEl) && command.Params.TryGetValue("content", out var contentEl))
                {
                    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                    var bytes = Convert.FromBase64String(contentEl.GetString() ?? "");
                    await storage.WritePluginFileAsync(command.CallerId, writePathEl.GetString() ?? "", bytes);
                    return new SidecarResponse { Success = true };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'path' or 'content' parameter" };

            case "fsListPluginFiles":
                if (command.Params.TryGetValue("path", out var listPathEl))
                {
                    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                    var dir = storage.GetPluginDataDirectory(command.CallerId);
                    var targetDir = Path.GetFullPath(Path.Combine(dir, listPathEl.GetString() ?? ""));
                    
                    if (!targetDir.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                        return new SidecarResponse { Success = false, Error = "Access Denied: Path outside sandbox" };

                    if (!Directory.Exists(targetDir))
                        return new SidecarResponse { Data = Array.Empty<string>() };

                    var files = Directory.GetFileSystemEntries(targetDir)
                        .Select(p => Path.GetRelativePath(dir, p))
                        .ToArray();
                        
                    return new SidecarResponse { Data = files };
                }
                return new SidecarResponse { Success = false, Error = "Missing 'path' parameter" };

            case "getDbMetadata":
                var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                return new SidecarResponse { 
                    Data = new { 
                        Path = dbService.DatabasePath,
                        DataDir = dbService.DataDirectory,
                        PluginsDir = dbService.PluginsDirectory,
                        TempDir = dbService.TempDirectory
                    } 
                };

            default:
                return new SidecarResponse { Success = false, Error = $"Unknown action: {command.Action}" };
        }
    }
    private static string? GetRequiredScope(string action) => action switch
    {
        "getConfig" => "sys:config:read",
        "updateConfig" => "sys:config:write",
        "setSetting" => "sys:config:write",
        "getThemes" => "sys:themes:read",
        "getThemeCss" => "sys:themes:read",
        "saveTheme" => "sys:themes:write",
        "getLoadedPlugins" => "sys:plugins:read",
        "getDiscoveredPlugins" => "sys:plugins:read",
        "getBeatmapSets" => "game:data:read",
        "searchBeatmaps" => "game:data:read",
        "fsReadPluginFile" => "sys:storage:read",
        "fsWritePluginFile" => "sys:storage:write",
        "fsListPluginFiles" => "sys:storage:read",
        _ => null
    };

    private static bool IsActionHostOnly(string action) => action switch
    {
        "importPackage" => true,
        "loadDevPlugin" => true,
        _ => false
    };
}

public class SidecarCommand
{
    public string Action { get; set; } = string.Empty;
    public string CallerId { get; set; } = "host";
    public Dictionary<string, JsonElement> Params { get; set; } = new();
}

public class SidecarResponse
{
    public bool Success { get; set; } = true;
    public object? Data { get; set; }
    public string? Error { get; set; }
}
