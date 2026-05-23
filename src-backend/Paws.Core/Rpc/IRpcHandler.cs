using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Paws.Abstractions.Models;

namespace Paws.Core.Rpc;

/// <summary>
/// Base interface for command handlers in the Sidecar RPC system.
/// </summary>
public interface IRpcHandler
{
    /// <summary>
    /// Returns true if this handler can process the given action namespace.
    /// </summary>
    bool CanHandle(string action);

    /// <summary>
    /// Processes the command and returns a response.
    /// </summary>
    Task<object?> HandleAsync(string action, string callerId, Dictionary<string, JsonElement> parameters);

    /// <summary>
    /// Gets the required scope for the action. Returns null if no scope is required.
    /// </summary>
    string? GetRequiredScope(string action);
    
    /// <summary>
    /// Returns true if the action is restricted to the host only.
    /// </summary>
    bool IsHostOnly(string action);
}
