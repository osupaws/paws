using Paws.Core.Abstractions.Interfaces;
using PawsHost = Paws.Core.Abstractions.Interfaces.Services.IHost;
using Paws.Host.Data.Schemas;
using System.Reflection;
using System.Runtime.Loader;
using Realms;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Core
{
    public class PluginManager
    {
        private readonly Dictionary<string, IPawsPlugin> _loadedPlugins = new();
        private readonly PawsHost _host;
        private readonly ILogger<PluginManager> _logger;
        private readonly PawsDbService _dbService;
        private readonly FileStorageService _storage;

        private readonly Dictionary<string, AssemblyLoadContext> _pluginContexts = new();

        public PluginManager(PawsHost host, ILogger<PluginManager> logger, PawsDbService dbService, FileStorageService storage)
        {
            _host = host;
            _logger = logger;
            _dbService = dbService;
            _storage = storage;
        }

        public async Task DiscoverAndLoadPluginsAsync()
        {
            _logger.LogInformation("Starting plugin discovery from database...");
            var allPlugins = _dbService.RunRead(realm => realm.All<Plugin>().ToList().Select(p => p.Freeze()).ToList());

            foreach (var plugin in allPlugins)
            {
                if (!plugin.IsActive)
                {
                    if (_loadedPlugins.ContainsKey(plugin.Id)) UnloadPlugin(plugin.Id);
                    continue;
                }
                if (_loadedPlugins.ContainsKey(plugin.Id)) continue;
                await LoadSinglePluginAsync(plugin);
            }
        }

        public async Task ReloadPluginAsync(string pluginId)
        {
            UnloadPlugin(pluginId);
            var plugin = _dbService.RunRead(realm => realm.Find<Plugin>(pluginId)?.Freeze());
            if (plugin != null && plugin.IsActive) await LoadSinglePluginAsync(plugin);
        }

        private void UnloadPlugin(string pluginId)
        {
            if (_loadedPlugins.Remove(pluginId, out var instance))
                _logger.LogInformation("Unloaded plugin instance: {Name}", instance.Name);

            if (_pluginContexts.Remove(pluginId, out var context))
                context.Unload();
        }

        private async Task LoadSinglePluginAsync(Plugin plugin)
        {
            try
            {
                if (string.IsNullOrEmpty(plugin.EntryPoint)) return;
                var context = new DbPluginLoadContext(_dbService, _storage, _logger, plugin.Id);
                var assemblyNameStr = Path.GetFileNameWithoutExtension(plugin.EntryPoint);
                var assemblyName = new AssemblyName(assemblyNameStr);
                var assembly = context.LoadFromAssemblyName(assemblyName);

                if (assembly == null) return;
                var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                if (pluginType == null) return;

                if (Activator.CreateInstance(pluginType) is IPawsPlugin pluginInstance)
                {
                    try
                    {
                        await pluginInstance.Initialize(_host);
                        _loadedPlugins[plugin.Id] = pluginInstance;
                        _pluginContexts[plugin.Id] = context;
                        _logger.LogInformation("Successfully loaded plugin: {Name} (v{Version})", pluginInstance.Name, pluginInstance.Version);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Plugin '{Name}' crashed during Initialize!", plugin.Name);
                        if (pluginInstance is IDisposable disposable) disposable.Dispose();
                        context.Unload();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while loading plugin '{Name}'.", plugin.Name);
            }
        }

        public IEnumerable<IPawsPlugin> GetLoadedPlugins() => _loadedPlugins.Values;

        public IEnumerable<PluginManifest> GetAllPlugins()
        {
            return _dbService.RunRead(realm =>
            {
                return realm.All<Plugin>().ToList().Select(p =>
                    new PluginManifest(p.Id, p.Name, p.Version, p.EntryPoint, p.Author, p.Description,
                        string.IsNullOrEmpty(p.UiEntry) ? null : new PluginUiManifest(p.UiEntry),
                        p.IconData, p.Permissions.ToList(), p.Provides.ToList(), p.Consumes.ToList(), p.IsActive)
                ).ToList();
            });
        }

        public async Task SetPluginActiveAsync(string pluginId, bool isActive)
        {
            _dbService.RunWrite(realm =>
            {
                var plugin = realm.Find<Plugin>(pluginId);
                if (plugin != null) plugin.IsActive = isActive;
            });
            if (isActive) await ReloadPluginAsync(pluginId);
            else UnloadPlugin(pluginId);
        }

        public IPawsPlugin? GetPluginById(string pluginId) => _loadedPlugins.Values.FirstOrDefault(p => p.Id == pluginId);
    }

    public record PluginManifest(string Id, string Name, string Version, string EntryPoint, string? Author, string? Description,
        PluginUiManifest? Ui, string? Icon, List<string>? Permissions = null, List<string>? Provides = null, List<string>? Consumes = null, bool IsActive = true);
    public record PluginUiManifest(string Entry);
}
