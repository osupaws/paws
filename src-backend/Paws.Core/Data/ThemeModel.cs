using Realms;
using System;

namespace Paws.Core.Data;

/// <summary>
/// Persistent database model for UI themes.
/// </summary>
public class ThemeModel : RealmObject
{
    [PrimaryKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = "";
    public string Author { get; set; } = "unknown";
    public string Description { get; set; } = "";

    // Hash of the CSS content, used to find the file in PawsData/data/
    public string? CssBlobHash { get; set; }

    public bool IsBuiltIn { get; set; } = false;
    public string BaseThemeId { get; set; } = "paws-dark";
}
