namespace Paws.Abstractions.Models;

public enum GameClientType
{
    None,
    Stable,
    Lazer
}

/// <summary>
/// Represents the current runtime state of the game client.
/// </summary>
public class HostState
{
    public bool IsOsuRunning { get; set; }
    public GameClientType ActiveClient { get; set; }
    public int ProcessId { get; set; }
    
    // TODO: Add background task status here in the future
    
    public bool HasChanged(HostState other)
    {
        if (other == null) return true;
        return IsOsuRunning != other.IsOsuRunning || 
               ActiveClient != other.ActiveClient || 
               ProcessId != other.ProcessId;
    }
}
