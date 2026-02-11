using Paws.Core.Abstractions.Interfaces;
using Paws.Core.Abstractions.Models;
using PawsHost = Paws.Core.Abstractions.Interfaces.Services.IHost;
using PawsLogger = Paws.Core.Abstractions.Interfaces.Services.ILogger;
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
        private readonly HostServices _globalHost;
        private readonly ILogger<PluginManager> _logger;
        private readonly PawsDbService _dbService;
        private readonly FileStorageService _storage;

        private readonly Dictionary<string, AssemblyLoadContext> _pluginContexts = new();

        public PluginManager(PawsHost host, ILogger<PluginManager> logger, PawsDbService dbService, FileStorageService storage)
        {
            _globalHost = (HostServices)host;
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
                        var osuPath = _dbService.GetSetting("osu.path")?.Value ?? string.Empty;

                        // Create contextual services for this plugin
                        // Use _globalHost as it implements PawsLogger
                        var storage = new StorageService(plugin.Id, plugin.Permissions.ToList(), osuPath, (PawsLogger)_globalHost);
                        var image = new ImageProcessorService(storage);

                        // Create contextual host
                        var pluginHost = new PluginHost(
                            (PawsLogger)_globalHost,
                            ((PawsHost)_globalHost).Lazer,
                            ((PawsHost)_globalHost).Stable,
                            storage,
                            image,
                            () => _globalHost.IsLegacyMode
                        );

                        await pluginInstance.Initialize((PawsHost)pluginHost);
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
                    new PluginManifest
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Version = p.Version,
                        EntryPoint = p.EntryPoint,
                        Author = p.Author,
                        Description = p.Description,
                        Permissions = p.Permissions.ToList(),
                        IsActive = p.IsActive,
                        Ui = string.IsNullOrEmpty(p.UiEntry) ? null : new PluginUiInfo { Entry = p.UiEntry }
                    }
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
}
