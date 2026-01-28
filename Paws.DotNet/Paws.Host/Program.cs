using Microsoft.AspNetCore.Mvc;
using Paws.Core.Abstractions;
using Paws.Host;
using System.Text;
using System.Text.Json;

// Must be registered to support legacy encodings in .osu files.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// --- Service Configuration ---
builder.WebHost.UseUrls("http://localhost:5088");

// Register Paws services as singletons to persist for the app's lifetime.
builder.Services.AddSingleton<PawsDbService>(); // Our main DB must be registered first
builder.Services.AddSingleton<FileStorageService>(); // Our file storage

// Register StableDbService and LazerDbService with their dependencies
builder.Services.AddSingleton<StableDbService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<StableDbService>>();
    var pawsDbService = sp.GetRequiredService<PawsDbService>();
    return new StableDbService(logger, pawsDbService);
});
builder.Services.AddSingleton<LazerDbService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LazerDbService>>();
    var pawsDbService = sp.GetRequiredService<PawsDbService>();
    return new LazerDbService(logger, pawsDbService);
});

builder.Services.AddSingleton<ThemeImporterService>(); // For importing themes
builder.Services.AddSingleton<PluginRepositoryService>(); // For the plugin store
builder.Services.AddSingleton<PluginManager>();
builder.Services.AddSingleton<PluginInstallerService>();
builder.Services.AddSingleton<IHostServices, HostServices>();
builder.Services.AddHttpClient(); // For PluginRepositoryService

var app = builder.Build();

// --- Asynchronous Service Initialization ---
var pawsDb = app.Services.GetRequiredService<PawsDbService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await pawsDb.InitializeAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "PawsDbService failed to initialize. The application will now exit.");
    // Exit gracefully if the main DB can't be opened.
    return;
}

// --- Plugin Loading ---
var pluginManager = app.Services.GetRequiredService<PluginManager>();
pluginManager.DiscoverAndLoadPlugins(); // Loads all active plugins from DB
logger.LogInformation("Paws.Host C# Backend started successfully.");

// --- API Endpoints ---

var api = app.MapGroup("/api");

// --- Theme Management Endpoints ---
var themesApi = api.MapGroup("/themes");
themesApi.MapGet("/", (PawsDbService db) => {
    // The service now returns DTOs directly, so no projection is needed here.
    return Results.Ok(db.GetAllThemes());
});
themesApi.MapPost("/import", async ([FromBody] ImportThemeRequest req, ThemeImporterService importer, ILogger<Program> endpointLogger) =>
{
    try
    {
        var importedTheme = await importer.ImportThemeAsync(req.FilePath);
        return Results.Ok(importedTheme);
    }
    catch (Exception ex)
    {
        // Log the full exception details to the backend console
        endpointLogger.LogError(ex, "An unhandled exception occurred during theme import for file: {FilePath}", req.FilePath);

        // Return a problem detail that includes the error message
        return Results.Problem(ex.Message, statusCode: 500);
    }
});


// --- File Serving Endpoint ---
api.MapGet("/files/{hash}", async (string hash, FileStorageService storage, PawsDbService db) =>
{
    var fileData = await storage.RetrieveFileAsync(hash);
    if (fileData == null)
    {
        return Results.NotFound();
    }

    var fileEntry = db.GetFileEntry(hash);
    var contentType = "application/octet-stream"; // Тип по умолчанию
    if (fileEntry != null)
    {
        contentType = fileEntry.Extension.ToLowerInvariant() switch
        {
            "css" => "text/css",
            "png" => "image/png",
            "jpg" => "image/jpeg",
            "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            _ => contentType
        };
    }

    return Results.Bytes(fileData, contentType);
});


// --- Path Management Endpoints ---
var pathsApi = api.MapGroup("/paths");
pathsApi.MapPost("/stable", ([FromBody] SetPathRequest req, StableDbService service) => {
    service.SetStablePath(req.Path);
    return Results.Ok();
});
pathsApi.MapPost("/lazer", ([FromBody] SetPathRequest req, LazerDbService service) => {
    service.SetLazerPath(req.Path);
    return Results.Ok();
});


// --- Configuration Management Endpoints (Legacy Adapter) ---
var configApi = api.MapGroup("/config");
configApi.MapGet("", (PawsDbService db) => {
    var stable = db.GetSetting("core.paths.stable")?.Value;
    var lazer = db.GetSetting("core.paths.lazer")?.Value;
    var legacy = db.GetSetting("core.modes.legacy")?.Value == "true";
    return Results.Ok(new { IsLegacyMode = legacy, StablePath = stable, LazerPath = lazer });
});
configApi.MapPost("", ([FromBody] UpdateConfigRequest req, PawsDbService db) => {
    if (req.IsLegacyMode.HasValue) db.SetSetting("core.modes.legacy", req.IsLegacyMode.Value.ToString().ToLower(), "bool");
    if (req.StablePath != null) db.SetSetting("core.paths.stable", req.StablePath, "string");
    if (req.LazerPath != null) db.SetSetting("core.paths.lazer", req.LazerPath, "string");
    return Results.Ok();
});

// --- Generic Settings Management Endpoints ---
var settingsApi = api.MapGroup("/settings");
settingsApi.MapGet("", (PawsDbService db) => Results.Ok(db.GetAllSettings()));
settingsApi.MapGet("{key}", (string key, PawsDbService db) => {
    var setting = db.GetSetting(key);
    return setting != null ? Results.Ok(setting) : Results.NotFound();
});
settingsApi.MapPost("", ([FromBody] UpdateSettingRequest req, PawsDbService db) => {
    db.SetSetting(req.Key, req.Value, req.Type);
    return Results.Ok();
});


// --- Plugin Management Endpoints ---
var pluginsApi = api.MapGroup("/plugins");

pluginsApi.MapPost("/install", async ([FromBody] InstallPluginRequest req, PluginInstallerService installer, PluginManager pm) => {
    try
    {
        var manifest = await installer.InstallPluginAsync(req.FilePath);

        // Reload plugins to pick up the new one (hot reload)
        pm.ReloadPlugin(manifest.Id);

        return Results.Ok(manifest);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

pluginsApi.MapPost("/toggle-active", ([FromBody] TogglePluginRequest req, PluginManager pm) =>
{
    pm.SetPluginActive(req.Id, req.IsActive);
    return Results.Ok();
});

pluginsApi.MapGet("/loaded", (PluginManager pm) => {
    // Return a DTO (Data Transfer Object) to control the data shape.
    var result = pm.GetLoadedPlugins().Select(p => {
        var manifest = pm.GetAllPlugins().FirstOrDefault(m => m.Id.Equals(p.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        return new
        {
            p.Id,
            p.Name,
            p.Version,
            p.Description,
            manifest?.Author,
            manifest?.Permissions,
            manifest?.Provides,
            manifest?.Consumes,
            Ui = manifest?.Ui
        };
    });
    return Results.Ok(result);
});

pluginsApi.MapGet("/discovered", (PluginManager pm) => Results.Ok(pm.GetAllPlugins()));
// pluginsApi.MapGet("/pending", (PluginManager pm) => Results.Ok(pm.GetPendingPlugins())); // Deprecated

pluginsApi.MapPost("/execute/{pluginId}", async (Guid pluginId, [FromBody] ExecuteCommandRequest req, PluginManager pm) => {
    var plugin = pm.GetPluginById(pluginId);
    if (plugin == null) return Results.NotFound($"Plugin with ID {pluginId} not found or not loaded.");
    if (string.IsNullOrEmpty(req.CommandName)) return Results.BadRequest("CommandName is required.");

    try
    {
        var result = await plugin.ExecuteCommandAsync(req.CommandName, req.Payload);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Serve plugin UI files (e.g. for paws-plugin protocol)
pluginsApi.MapGet("/{pluginId}/files/{*path}", (string pluginId, string path, PawsDbService db) => {
    var config = db.GetRealmConfiguration();
    using var realm = Realms.Realm.GetInstance(config);

    var plugin = realm.Find<Paws.Host.Data.Schemas.Plugin>(pluginId);
    if (plugin == null) return Results.NotFound();

    // Mapping strategy: The Paws UI protocol serves from the 'ui' folder by default.
    // We try to find 'ui/{path}' first, then '{path}'.
    // Using simple linear search for now as file counts per plugin are small.
    var targetPath = path.Replace("\\", "/");

    var file = plugin.Files.FirstOrDefault(f => f.VirtualPath.Equals($"ui/{targetPath}", StringComparison.OrdinalIgnoreCase))
            ?? plugin.Files.FirstOrDefault(f => f.VirtualPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase));

    if (file == null) return Results.NotFound();

    var blob = realm.Find<Paws.Host.Data.Schemas.FileBlob>(file.BlobHash);
    if (blob == null) return Results.NotFound();

    var ext = Path.GetExtension(targetPath).ToLowerInvariant().TrimStart('.');
    var contentType = ext switch
    {
        "html" => "text/html",
        "css" => "text/css",
        "js" => "application/javascript",
        "json" => "application/json",
        "png" => "image/png",
        "jpg" => "image/jpeg",
        "jpeg" => "image/jpeg",
        "svg" => "image/svg+xml",
        "woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    return Results.Bytes(blob.Data, contentType);
});

// Endpoint for the future plugin store feature
pluginsApi.MapGet("/store", async (PluginRepositoryService repoService) => {
    var availablePlugins = await repoService.GetAvailablePluginsAsync();
    return Results.Ok(availablePlugins);
});

app.Run();

// --- API Request/Response Records ---
public record SetPathRequest(string Path);
public record ImportThemeRequest(string FilePath);
public record ExecuteCommandRequest(string CommandName, object? Payload);
public record UpdateConfigRequest(bool? IsLegacyMode, string? StablePath, string? LazerPath);
public record UpdateSettingRequest(string Key, string Value, string Type = "string");
public record InstallPluginRequest(string FilePath);
public record TogglePluginRequest(string Id, bool IsActive);
