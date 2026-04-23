using Paws.Abstractions.Models;
using Paws.Abstractions.Services;
using Paws.Core.Data;
using Paws.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Paws.Core.Services;

public class ThemeService : IThemeService
{
    private readonly IDatabaseService _db;
    
    public ThemeService(IDatabaseService db)
    {
        _db = db;
        InitializeBuiltInThemes(); // Инициализируем при создании
    }

    private void InitializeBuiltInThemes()
    {
        using var realm = _db.GetRealm();
        
        realm.Write(() => {
            if (realm.Find<ThemeModel>("paws-dark") == null)
            {
                realm.Add(new ThemeModel { 
                    Id = "paws-dark", 
                    Name = "Paws Dark", 
                    IsBuiltIn = true,
                    BaseThemeId = "paws-dark"
                });
            }
            if (realm.Find<ThemeModel>("paws-light") == null)
            {
                realm.Add(new ThemeModel { 
                    Id = "paws-light", 
                    Name = "Paws Light", 
                    IsBuiltIn = true,
                    BaseThemeId = "paws-light"
                });
            }
        });
    }

    public async Task<Theme?> GetThemeAsync(string id)
    {
        using var realm = _db.GetRealm();
        var model = realm.Find<ThemeModel>(id);
        if (model == null) return null;

        string? css = null;
        if (!string.IsNullOrEmpty(model.CssBlobHash))
        {
            var filePath = Path.Combine(_db.DataDirectory, model.CssBlobHash);
            if (File.Exists(filePath))
            {
                css = await File.ReadAllTextAsync(filePath);
            }
        }

        return new Theme {
            Id = model.Id,
            Name = model.Name,
            Author = model.Author,
            Description = model.Description,
            IsBuiltIn = model.IsBuiltIn,
            BaseThemeId = model.BaseThemeId,
            BlobHash = model.CssBlobHash,
            Css = css
        };
    }

    public Task<IEnumerable<Theme>> GetAllThemesAsync()
    {
        using var realm = _db.GetRealm();
        var themes = realm.All<ThemeModel>().ToList().Select(model => new Theme {
            Id = model.Id,
            Name = model.Name,
            Author = model.Author,
            Description = model.Description,
            IsBuiltIn = model.IsBuiltIn,
            BaseThemeId = model.BaseThemeId,
            BlobHash = model.CssBlobHash
            // CSS контент не грузим для списка, чтобы не забивать память
        }).ToList();
        
        return Task.FromResult<IEnumerable<Theme>>(themes);
    }

    public async Task AddThemeAsync(Theme theme)
    {
        if (string.IsNullOrEmpty(theme.Id))
            throw new ArgumentException("Theme ID cannot be empty");

        // Защита: нельзя перезаписывать встроенные темы
        if (theme.Id.StartsWith("paws-") || theme.IsBuiltIn)
        {
            using var r = _db.GetRealm();
            var existing = r.Find<ThemeModel>(theme.Id);
            if (existing != null && existing.IsBuiltIn)
            {
                throw new InvalidOperationException($"Cannot overwrite built-in theme '{theme.Id}'");
            }
        }

        string? hash = null;
        if (!string.IsNullOrEmpty(theme.Css))
        {
            hash = HashHelper.ComputeSha256(theme.Css);
            string filePath = Path.Combine(_db.DataDirectory, hash);

            if (!File.Exists(filePath))
            {
                await File.WriteAllTextAsync(filePath, theme.Css);
            }
        }

        using var realm = _db.GetRealm();
        await realm.WriteAsync(() => {
            realm.Add(new ThemeModel {
                Id = theme.Id,
                Name = theme.Name,
                Author = theme.Author,
                Description = theme.Description,
                CssBlobHash = hash,
                IsBuiltIn = false, // Кастомная тема никогда не может стать встроенной через API
                BaseThemeId = string.IsNullOrEmpty(theme.BaseThemeId) ? "paws-dark" : theme.BaseThemeId
            }, update: true);
        });
    }

    public async Task DeleteThemeAsync(string id)
    {
        using var realm = _db.GetRealm();
        var model = realm.Find<ThemeModel>(id);
        if (model != null && !model.IsBuiltIn)
        {
            await realm.WriteAsync(() => {
                realm.Remove(model);
            });
        }
    }
}
