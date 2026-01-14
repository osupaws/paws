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
pluginManager.DiscoverAndLoadPlugins(Enumerable.Empty<string>()); // Step 1: Discover all plugins.
var discoveredIds = pluginManager.GetDiscoveredPlugins().Select(p => p.Id).ToList();
pluginManager.DiscoverAndLoadPlugins(discoveredIds); // Step 2: Load all discovered plugins.
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


// --- Plugin Management Endpoints ---
var pluginsApi = api.MapGroup("/plugins");

pluginsApi.MapGet("/loaded", (PluginManager pm) => {
    // Return a DTO (Data Transfer Object) to control the data shape.
    var result = pm.GetLoadedPlugins().Select(p => {
        var manifest = pm.GetDiscoveredPlugins().FirstOrDefault(m => m.Id.Equals(p.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        return new { p.Id, p.Name, p.Version, p.Description, Ui = manifest?.Ui };
    });
    return Results.Ok(result);
});

pluginsApi.MapGet("/discovered", (PluginManager pm) => Results.Ok(pm.GetDiscoveredPlugins()));
pluginsApi.MapGet("/pending", (PluginManager pm) => Results.Ok(pm.GetPendingPlugins()));

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