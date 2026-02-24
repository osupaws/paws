using System;

namespace Paws.Core.Abstractions.Interfaces
{
    public interface IPlugin
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Version { get; }
        string? Author => null;
    }
}
