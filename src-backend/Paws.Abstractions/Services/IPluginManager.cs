using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Models;

namespace Paws.Abstractions.Services;

public interface IPluginManager
{
    // Запускает сканирование папки Data/Plugins/ и загружает манифесты и DLL-модули
    Task LoadPluginsAsync();
    
    // Возвращает список всех найденных плагинов 
    IEnumerable<PluginManifest> GetLoadedPlugins();
    
    // Возвращает манифест по ID плагина (проверка версий и т.д.)
    PluginManifest? GetManifest(string pluginId);

    // Cross-Plugin API: Вызывает метод [PublicEntryPoint] из загруженного плагина по имени
    Task<object?> InvokePluginMethodAsync(string sourcePluginId, string targetPluginId, string method, Dictionary<string, object>? args);

    // Загрузка плагина-кандидата из произвольной папки (Developer Hotplug)
    Task LoadDevPluginAsync(string absolutePathToFolder);

    // Горячая выгрузка плагина из памяти ОС (для перезагрузки или отключения)
    Task UnloadPluginAsync(string pluginId);
}
