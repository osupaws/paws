namespace Paws.Host.Data
{
    // A plain C# object (Data Transfer Object) to represent a FileEntry.
    public record FileEntryDto(string Hash, int Size, string Extension);
    
    // A DTO to represent a Theme, safe to be serialized and sent over the network.
    public record ThemeDto(
        string Id,
        string Name,
        string Base,
        string? Author,
        string? Version,
        FileEntryDto? File
    );
}
