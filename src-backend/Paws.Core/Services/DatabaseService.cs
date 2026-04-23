using Paws.Abstractions.Services;
using Realms;
using System;
using System.IO;

namespace Paws.Core.Services;

public class DatabaseService : IDatabaseService
{
    public string DatabasePath { get; private set; }
    public string DataDirectory { get; private set; }
    public string PluginsDirectory { get; private set; }
    public string TempDirectory { get; private set; }

    private readonly RealmConfiguration _config;

    public DatabaseService()
    {
        // 1. Определяем корневой путь PawsData
        var rootDataPath = ResolveRootDataPath();

        // 2. Инициализируем пути внутри PawsData
        DatabasePath = Path.Combine(rootDataPath, "paws.realm");
        DataDirectory = Path.Combine(rootDataPath, "data");
        PluginsDirectory = Path.Combine(rootDataPath, "plugins");
        TempDirectory = Path.Combine(rootDataPath, "temp");

        // 3. Создаем структуру
        EnsureStructureExists();

        _config = new RealmConfiguration(DatabasePath)
        {
            SchemaVersion = 2,
            // Здесь можно добавить миграции в будущем
        };
    }

    public Realm GetRealm()
    {
        return Realm.GetInstance(_config);
    }

    private string ResolveRootDataPath()
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var baseDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

        string portableDataPath = Path.Combine(baseDir, "PawsData");

        // 1. Если папка PawsData уже существует рядом с EXE - используем её
        if (Directory.Exists(portableDataPath))
        {
            return portableDataPath;
        }

        // 2. Если папки нет, проверяем, можем ли мы её создать (права на запись в baseDir)
        try
        {
            // Пытаемся создать тестовую папку и тут же удалить её
            string testDirPath = Path.Combine(baseDir, ".paws_write_test");
            Directory.CreateDirectory(testDirPath);
            Directory.Delete(testDirPath);

            // Если получилось - создаем и возвращаем путь к PawsData в текущей папке
            return portableDataPath;
        }
        catch
        {
            // 3. Если прав нет (например, Program Files) - уходим в AppData/Local
            var appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataLocal, "PawsData");
        }
    }

    private void EnsureStructureExists()
    {
        var dirs = new[] {
            Path.GetDirectoryName(DatabasePath),
            DataDirectory,
            PluginsDirectory,
            TempDirectory
        };

        foreach (var dir in dirs)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        // Очистка временных файлов сессии
        try
        {
            if (Directory.Exists(TempDirectory))
            {
                foreach (var file in Directory.GetFiles(TempDirectory))
                {
                    File.Delete(file);
                }
            }
        }
        catch { /* Ошибки очистки темпа не критичны для запуска */ }
    }
}
