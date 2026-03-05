using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paws.Core.Abstractions.Interfaces.Services;
using IStorageService = Paws.Core.Abstractions.Interfaces.Services.IStorageService;
using ILogger = Paws.Core.Abstractions.Interfaces.Services.ILogger;
using Paws.Host.Services.Lazer;
using Paws.Host.Services.Stable;

namespace Paws.Host.Services.Core
{
    public class StorageService : IStorageService
    {
        private readonly string _baseDataPath;
        private readonly string _baseTempPath;
        private readonly string _assetsPath;
        private readonly string _pluginId;
        private readonly List<string> _permissions;
        private readonly string _lazerPath;
        private readonly string _stablePath;
        private readonly ILogger _logger;

        private readonly string _globalTempPath;
        private DateTime _lastProcessCheck = DateTime.MinValue;
        private bool _isGameRunningCached = false;

        public StorageService(
            string pluginId,
            List<string> permissions,
            string lazerPath,
            string stablePath,
            ILogger logger)
        {
            _pluginId = pluginId;
            _permissions = permissions ?? new List<string>();
            _lazerPath = lazerPath;
            _stablePath = stablePath;
            _logger = logger;

            // Paths: %AppData%/Paws/Plugins/{ID}/Data
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseDataPath = Path.Combine(appData, "Paws", "Plugins", _pluginId, "Data");

            // Paths: %Temp%/Paws/Plugins/{ID}
            _baseTempPath = Path.Combine(Path.GetTempPath(), "Paws", "Plugins", _pluginId);

            // Global Assets Path: %AppData%/Paws/Host/Assets/Managed
            _assetsPath = Path.Combine(appData, "Paws", "Host", "Assets", "Managed");

            // GLOBAL TEMP: %AppData%/Paws/Host/Temp
            _globalTempPath = Path.Combine(appData, "Paws", "Host", "Temp");

            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(_baseDataPath)) Directory.CreateDirectory(_baseDataPath);
            if (!Directory.Exists(_baseTempPath)) Directory.CreateDirectory(_baseTempPath);
            if (!Directory.Exists(_assetsPath)) Directory.CreateDirectory(_assetsPath);
            if (!Directory.Exists(_globalTempPath)) Directory.CreateDirectory(_globalTempPath);

            // Only primary system service cleans up temp
            if (_pluginId == "System")
            {
                CleanupTempFiles();
            }
        }

        private void CleanupTempFiles()
        {
            try
            {
                if (Directory.Exists(_globalTempPath))
                {
                    foreach (var file in Directory.GetFiles(_globalTempPath))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Failed to cleanup temp files: {ex.Message}", Paws.Core.Abstractions.Enums.PawsLogLvl.Warning);
            }
        }

        public string GetPluginDataPath() => _baseDataPath;
        public string GetPluginTempPath() => _baseTempPath;

        public async Task<string> StoreTempByPathAsync(string sourcePath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source file not found", sourcePath);

            var handle = Guid.NewGuid().ToString("N");
            var path = Path.Combine(_globalTempPath, handle);

            using (var sourceStream = File.OpenRead(sourcePath))
            using (var destStream = File.Create(path))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            return handle;
        }

        public async Task<string> StoreTempAsync(Stream stream)
        {
            var handle = Guid.NewGuid().ToString("N");
            var path = Path.Combine(_globalTempPath, handle);

            using (var fileStream = File.Create(path))
            {
                await stream.CopyToAsync(fileStream);
            }

            return handle;
        }

        public Stream OpenTempStream(string handle)
        {
            var path = Path.Combine(_globalTempPath, handle);
            if (!File.Exists(path)) throw new FileNotFoundException("Temp file not found", handle);
            return File.OpenRead(path);
        }

        public void MoveTempToData(string handle, string targetPath)
        {
            var sourcePath = Path.Combine(_globalTempPath, handle);
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Temp file not found", handle);

            ValidateAccess(targetPath, FileAccess.Write);

            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(targetPath);
            if (destDir != null && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Move(sourcePath, targetPath);
        }

        public async Task CopyDirectoryAsync(string sourcePath, string destinationPath, bool recursive = true)
        {
            ValidateAccess(sourcePath, FileAccess.Read);
            ValidateAccess(destinationPath, FileAccess.Write);

            var dir = new DirectoryInfo(sourcePath);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

            var dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationPath);

            foreach (var file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationPath, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (var subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationPath, subDir.Name);
                    await CopyDirectoryAsync(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        public void MoveDirectory(string sourcePath, string destinationPath)
        {
            ValidateAccess(sourcePath, FileAccess.Write);
            ValidateAccess(destinationPath, FileAccess.Write);

            if (!Directory.Exists(sourcePath)) throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");

            if (Directory.Exists(destinationPath)) Directory.Delete(destinationPath, true);
            Directory.Move(sourcePath, destinationPath);
        }

        public async Task<string> StoreAssetAsync(Stream stream, string extension)
        {
            var assetId = Guid.NewGuid().ToString();
            var fileName = $"{assetId}.{extension.TrimStart('.')}";
            var path = Path.Combine(_assetsPath, fileName);

            using (var fileStream = File.Create(path))
            {
                await stream.CopyToAsync(fileStream);
            }

            return assetId;
        }

        public Stream GetAssetStream(string assetId)
        {
            // Find file in assets path starting with assetId
            var file = Directory.GetFiles(_assetsPath, $"{assetId}.*").FirstOrDefault();
            if (file == null) throw new FileNotFoundException("Asset not found", assetId);

            return File.OpenRead(file);
        }

        public bool FileExists(string path)
        {
            ValidateAccess(path, FileAccess.Read);
            return File.Exists(path);
        }

        public Stream OpenFile(string path, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read)
        {
            ValidateAccess(path, access);

            // Ensure directory exists for write modes
            if (mode == FileMode.Create || mode == FileMode.OpenOrCreate || mode == FileMode.CreateNew)
            {
                var dir = Path.GetDirectoryName(path);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }

            return File.Open(path, mode, access);
        }

        public void DeleteFile(string path)
        {
            ValidateAccess(path, FileAccess.Write);
            if (File.Exists(path)) File.Delete(path);
        }

        public bool DirectoryExists(string path)
        {
            ValidateAccess(path, FileAccess.Read);
            return Directory.Exists(path);
        }

        public void DeleteDirectory(string path, bool recursive = false)
        {
            ValidateAccess(path, FileAccess.Write);
            if (Directory.Exists(path)) Directory.Delete(path, recursive);
        }

        public long GetFileLength(string path)
        {
            ValidateAccess(path, FileAccess.Read);
            return new FileInfo(path).Length;
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            ValidateAccess(path, FileAccess.Read);
            return File.GetLastWriteTimeUtc(path);
        }

        public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            ValidateAccess(path, FileAccess.Read);
            return Directory.GetFiles(path, searchPattern, searchOption);
        }

        public string[] GetDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            ValidateAccess(path, FileAccess.Read);
            return Directory.GetDirectories(path, searchPattern, searchOption);
        }

        private void ValidateAccess(string path, FileAccess access)
        {
            var fullPath = Path.GetFullPath(path);

            // 1. Always allow access to plugin's own directories
            if (fullPath.StartsWith(_baseDataPath, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(_baseTempPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // 2. Check filesystem-ext (Unrestricted)
            if (_permissions.Contains("filesystem-ext"))
            {
                return;
            }

            // 3. Check filesystem-osu (Access to osu! folder)
            if (_permissions.Contains("filesystem-osu"))
            {
                bool isOsuPath = false;
                if (!string.IsNullOrEmpty(_lazerPath) && fullPath.StartsWith(_lazerPath, StringComparison.OrdinalIgnoreCase))
                {
                    isOsuPath = true;
                }
                else if (!string.IsNullOrEmpty(_stablePath) && fullPath.StartsWith(_stablePath, StringComparison.OrdinalIgnoreCase))
                {
                    isOsuPath = true;
                }

                if (isOsuPath)
                {
                    // Automatic protection: block writes if game is running
                    if (access != FileAccess.Write && access != FileAccess.ReadWrite) // Check if write access is requested
                        return;

                    if (DateTime.Now - _lastProcessCheck > TimeSpan.FromSeconds(1))
                    {
                        // Identify which game path we are in
                        bool isLazer = !string.IsNullOrEmpty(_lazerPath) && fullPath.StartsWith(_lazerPath, StringComparison.OrdinalIgnoreCase);
                        bool isStable = !string.IsNullOrEmpty(_stablePath) && fullPath.StartsWith(_stablePath, StringComparison.OrdinalIgnoreCase);

                        if (isLazer)
                        {
                            _isGameRunningCached = LazerDbService.IsLazerRunning();
                            if (_isGameRunningCached) throw new Paws.Core.Abstractions.Exceptions.LazerIsRunningException();
                        }
                        else if (isStable)
                        {
                            _isGameRunningCached = StableDbService.IsStableRunning();
                            if (_isGameRunningCached) throw new Paws.Core.Abstractions.Exceptions.StableIsRunningException();
                        }

                        _lastProcessCheck = DateTime.Now;
                    }
                    return;
                }
            }

            // 4. Deny by default
            _logger.LogMessage($"Access Denied: Plugin '{_pluginId}' tried to access unauthorized path '{fullPath}'", Paws.Core.Abstractions.Enums.PawsLogLvl.Warning);
            throw new UnauthorizedAccessException($"Plugin does not have permission to access path: {path}");
        }
    }
}
