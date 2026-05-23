using System;

namespace Paws.Abstractions.Plugins;

/// <summary>
/// Marks a public method as an entry point accessible via Cross-Plugin API (InvokePluginAsync).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class PublicEntryPointAttribute : Attribute
{
    public string MethodName { get; }

    /// <param name="methodName">Optional custom method name. If null, the C# method name is used.</param>
    public PublicEntryPointAttribute(string? methodName = null)
    {
        MethodName = methodName ?? string.Empty;
    }
}
