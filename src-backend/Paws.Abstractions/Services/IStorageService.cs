namespace Paws.Abstractions.Services;

public interface IStorageService
{
    // Универсальные Blob'ы (картинки, темы)
    Task<string> SaveBlobAsync(byte[] data, string contentType);
    Task<byte[]?> GetBlobAsync(string hash);
    Task<string?> GetBlobPathAsync(string hash);
    void DeleteBlob(string hash);

    // Песочница плагинов (Sandbox)
    string GetPluginDataDirectory(string pluginId);
    Task<byte[]> ReadPluginFileAsync(string pluginId, string relativePath);
    Task WritePluginFileAsync(string pluginId, string relativePath, byte[] data);
    
    // Проверка разрешений хоста (File Scopes)
    bool ValidatePathAccess(string pluginId, string absolutePath);
}
