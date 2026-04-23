using Realms;
using System;

namespace Paws.Core.Data;

public class ThemeModel : RealmObject
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string Name { get; set; } = "";
    public string Author { get; set; } = "unknown";
    public string Description { get; set; } = "";
    
    // Хеш контента CSS, по которому мы будем искать файл в PawsData/data/
    public string? CssBlobHash { get; set; }
    
    public bool IsBuiltIn { get; set; } = false;
    
    // Имя базовой темы (dark/light) для правильной отрисовки UI компонентов
    public string BaseThemeId { get; set; } = "paws-dark";
}
