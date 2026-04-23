namespace Paws.Abstractions.Models;

public enum GameClientType
{
    None,
    Stable,
    Lazer
}

public class HostState
{
    public bool IsOsuRunning { get; set; }
    public GameClientType ActiveClient { get; set; }
    public int ProcessId { get; set; }
    
    // В будущем сюда можно добавить статусы фоновых задач
    
    public bool HasChanged(HostState other)
    {
        if (other == null) return true;
        return IsOsuRunning != other.IsOsuRunning || 
               ActiveClient != other.ActiveClient || 
               ProcessId != other.ProcessId;
    }
}
