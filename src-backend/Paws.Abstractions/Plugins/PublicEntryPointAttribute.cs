using System;

namespace Paws.Abstractions.Plugins;

/// <summary>
/// Атрибут для пометки публичных методов в плагинах, доступных через шину Cross-Plugin API (InvokePluginAsync).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PublicEntryPointAttribute : Attribute
{
    public string MethodName { get; }

    /// <param name="methodName">Опциональное кастомное имя метода. Если не указано, используется имя метода C#.</param>
    public PublicEntryPointAttribute(string? methodName = null)
    {
        MethodName = methodName ?? string.Empty;
    }
}
