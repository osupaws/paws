using Paws.Abstractions.Services;
using Realms;
using System;
using System.IO;

namespace Paws.Core.Services;

/// <summary>
/// Service responsible for initializing and providing access to the local Paws Realm database.
/// Automatically determines the root data path based on portability and write permissions.
/// </summary>
public class DatabaseService : IDatabaseService
{
    public string DatabasePath { get; private set; }
    public string DataDirectory { get; private set; }
    public string PluginsDirectory { get; private set; }
    public string TempDirectory { get; private set; }

    private readonly RealmConfiguration _config;

    public DatabaseService()
    {
        // 1. Resolve the PawsData root path
        var rootDataPath = ResolveRootDataPath();

        // 2. Initialize internal paths
        DatabasePath = Path.Combine(rootDataPath, "paws.realm");
        DataDirectory = Path.Combine(rootDataPath, "data");
        PluginsDirectory = Path.Combine(rootDataPath, "plugins");
        TempDirectory = Path.Combine(rootDataPath, "temp");

        // 3. Create folder structure
        EnsureStructureExists();

        _config = new RealmConfiguration(DatabasePath)
        {
            SchemaVersion = 2,
            // Future migrations go here
        };
    }

    public Realm GetRealm()
    {
        return Realm.GetInstance(_config);
    }

    private string ResolveRootDataPath()
    {
        // 0. Use Current Working Directory if PawsData exists there
        var cwd = Environment.CurrentDirectory;
        string cwdDataPath = Path.Combine(cwd, "PawsData");
        if (Directory.Exists(cwdDataPath))
        {
            return cwdDataPath;
        }

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        var baseDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

        string portableDataPath = Path.Combine(baseDir, "PawsData");

        // 1. Use PawsData if it exists next to the EXE (Portable mode)
        if (Directory.Exists(portableDataPath))
        {
            return portableDataPath;
        }

        // 2. Check for write permissions in the EXE directory
        try
        {
            // Attempt to create a test directory
            string testDirPath = Path.Combine(baseDir, ".paws_write_test");
            Directory.CreateDirectory(testDirPath);
            Directory.Delete(testDirPath);

            // Success: use portable PawsData in the current folder
            return portableDataPath;
        }
        catch
        {
            // 3. No write permissions: use AppData/Local (System installation)
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

        // Clean up temporary session files
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
        catch { /* Temp cleanup errors are non-critical */ }
    }
}
