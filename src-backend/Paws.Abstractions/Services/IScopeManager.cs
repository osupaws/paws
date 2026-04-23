using System.Collections.Generic;

namespace Paws.Abstractions.Services;

public interface IScopeManager
{
    // Проверка статического права из plugin.json (например, "fs:lazer:write")
    bool HasScope(string pluginId, string scopeName);
    
    // Выдача плагину динамического доступа к пользовательской папке (те самые 10 ГБ ассетов)
    void GrantRuntimeScope(string pluginId, string folderPath);
    
    // Получение списка всех разрешенных динамических папок для валидации путей
    IEnumerable<string> GetRuntimeAllowedFolders(string pluginId);
}
