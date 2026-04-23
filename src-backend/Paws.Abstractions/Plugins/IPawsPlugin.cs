using System.Threading.Tasks;

namespace Paws.Abstractions.Plugins;

public interface IPawsPlugin
{
    // Инициализация плагина (вызывается ядром при загрузке сборки)
    Task InitializeAsync(IHostApi api);
    
    // Мягкая остановка (при выключении плагина или ядра)
    Task ShutdownAsync();
}
