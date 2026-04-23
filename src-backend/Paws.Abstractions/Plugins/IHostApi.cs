using System.Collections.Generic;
using System.Threading.Tasks;
using Paws.Abstractions.Services;

namespace Paws.Abstractions.Plugins;

/// <summary>
/// Фасад для общения плагина с ядром (Sidecar).
/// Плагин не получает прямых ссылок на системные синглтоны Ядра для своей же безопасности.
/// Ядро инжектит реализации, в которые уже 'вшит' ID текущего плагина (Currying).
/// </summary>
public interface IHostApi
{
    /// <summary>
    /// Безопасный доступ к файлам только в пределах песочницы плагина (или выданных Scopes).
    /// Плагин не передает свой ID, это делает фасадная обертка Ядра под капотом.
    /// </summary>
    ISandboxedStorage Storage { get; }

    /// <summary>
    /// Базах данных игры (Lazer/Stable). Ядро автоматически читает из нужной БД.
    /// </summary>
    IGameDataService GameData { get; }

    /// <summary>
    /// Состояние игры (включена/выключена, путь).
    /// </summary>
    IMonitoringService Monitor { get; }

    /// <summary>
    /// Вызов публичного метода (Cross-Plugin API Bridge) другого плагина (если есть права в scopes: api:plugin:TARGET).
    /// </summary>
    Task<object?> InvokePluginAsync(string targetPluginId, string method, Dictionary<string, object>? args = null);
}

public interface ISandboxedStorage
{
    /// <summary>
    /// Чтение файла из папки Data/Plugins/{Ваш плагин}/Data...
    /// Защищено от '../' (Path Traversal) и не принимает абсолютные пути.
    /// </summary>
    Task<byte[]> ReadFileAsync(string relativePath);

    /// <summary>
    /// Запись файла в папку изолированных данных плагина.
    /// </summary>
    Task WriteFileAsync(string relativePath, byte[] data);

    /// <summary>
    /// Попытка прочитать файл по жесткому пути на диске. 
    /// Сработает ТОЛЬКО если у плагина есть Scope на fs:stable:read, fs:lazer:read или Runtime-Scoope 'D:\MyFolder'.
    /// Иначе выкинет UnauthorizedAccessException.
    /// </summary>
    Task<byte[]> ReadAbsolutePathAsync(string absolutePath);
}
