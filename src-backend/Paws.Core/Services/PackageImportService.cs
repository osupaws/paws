using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;

namespace Paws.Core.Services;

public class PackageImportService : IPackageImportService
{
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PackageImportService(IThemeService themeService, IStorageService storageService)
    {
        _themeService = themeService;
        _storageService = storageService;
    }

    public async Task<bool> ImportPackageAsync(string archiveFilePath)
    {
        if (!File.Exists(archiveFilePath)) return false;

        try
        {
            using var archive = ZipFile.OpenRead(archiveFilePath);
            var manifestEntry = archive.GetEntry("manifest.json") ?? archive.GetEntry("theme.json");

            if (manifestEntry == null)
            {
                throw new Exception("Metadata (manifest.json or theme.json) not found in the package.");
            }

            using var manifestStream = manifestEntry.Open();
            var manifest = await JsonSerializer.DeserializeAsync<PackageManifest>(manifestStream, _jsonOptions);

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new Exception("Invalid or empty manifest in the package.");
            }

            // Автодетекция типа, если он не указан явно (например .pawstheme -> "theme")
            if (string.IsNullOrEmpty(manifest.Type))
            {
                if (archiveFilePath.EndsWith(".pawstheme", StringComparison.OrdinalIgnoreCase))
                    manifest.Type = "theme";
                else if (archiveFilePath.EndsWith(".pawsplugin", StringComparison.OrdinalIgnoreCase))
                    manifest.Type = "plugin";
            }

            if (manifest.Type.Equals("theme", StringComparison.OrdinalIgnoreCase))
            {
                return await ImportThemeAsync(manifest, archive);
            }
            else if (manifest.Type.Equals("plugin", StringComparison.OrdinalIgnoreCase))
            {
                // Плагины будем внедрять позже - там сложнее с DLL
                throw new NotImplementedException("Plugin imports are not implemented yet.");
            }
            else
            {
                throw new Exception($"Unknown package type: {manifest.Type}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PackageImportService] Failed to import package: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ImportThemeAsync(PackageManifest manifest, ZipArchive archive)
    {
        // 1. Находим точку входа CSS (по умолчанию theme.css)
        var entryName = !string.IsNullOrEmpty(manifest.Entry) ? manifest.Entry : "theme.css";
        var entryFile = archive.GetEntry(entryName);
        
        if (entryFile == null)
        {
            throw new Exception($"Theme entry file '{entryName}' not found in the archive.");
        }

        string cssContent;
        using (var entryStream = entryFile.Open())
        using (var reader = new StreamReader(entryStream))
        {
            cssContent = await reader.ReadToEndAsync();
        }

        // 2. Ищем и заменяем ссылки на ассеты (картинки, шрифты) внутри CSS на блоб-протокол
        // Регулярка для url('path'), url("path"), url(path)
        var urlRegex = new Regex(@"url\(['""]?(.*?)['""]?\)");
        var processedAssets = new Dictionary<string, string>();

        var matches = urlRegex.Matches(cssContent);
        foreach (Match match in matches)
        {
            if (match.Groups.Count < 2) continue;
            var assetPath = match.Groups[1].Value;

            // Игнорируем внешние ссылки и data URL
            if (assetPath.StartsWith("http://") || assetPath.StartsWith("https://") || assetPath.StartsWith("data:"))
            {
                continue;
            }

            // Если ассет уже обработан (кеш хеша)
            if (processedAssets.TryGetValue(assetPath, out var cachedHash))
            {
                cssContent = cssContent.Replace(match.Captures[0].Value, $"url('pawstheme://{cachedHash}')");
                continue;
            }

            // Ищем ассет внутри архива (относительно корня)
            var assetEntry = archive.GetEntry(assetPath);
            if (assetEntry != null)
            {
                using var assetStream = assetEntry.Open();
                using var ms = new MemoryStream();
                await assetStream.CopyToAsync(ms);

                // Сохраняем в папку data/ под хешем
                var hash = await _storageService.SaveBlobAsync(ms.ToArray(), "application/octet-stream");
                
                processedAssets[assetPath] = hash;
                
                // В CSS ссылаемся на протокол, который Tauri подхватит из файла напрямую
                cssContent = cssContent.Replace(match.Captures[0].Value, $"url('pawstheme://{hash}')");
                
                Console.WriteLine($"[PackageImportService] Theme asset '{assetPath}' imported as blob: {hash}");
            }
        }

        // 3. Последним шагом - регистрируем тему в ThemeService через наш новый API
        var theme = new Theme
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            Author = manifest.Author,
            BaseThemeId = manifest.BaseThemeId ?? "paws-dark",
            IsBuiltIn = false,
            Css = cssContent // CSS теперь содержит ссылки на локальные блобы
        };

        await _themeService.AddThemeAsync(theme);
        Console.WriteLine($"[PackageImportService] Imported theme '{manifest.Name}' [{manifest.Id}] successfully.");
        return true;
    }
}
