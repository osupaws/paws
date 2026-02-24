using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Paws.Host.Services.Core
{
    public class FileStorageService
    {
        private readonly ILogger<FileStorageService> _logger;
        private readonly PawsDbService _dbService;

        public FileStorageService(ILogger<FileStorageService> logger, PawsDbService dbService)
        {
            _logger = logger;
            _dbService = dbService;
        }

        private string GetStorageDir()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(appData, "Paws", "Host", "Data", "Storage");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public async Task<string> StoreFileAsync(byte[] data, string extension)
        {
            string hash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(data);
                hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            var dir = GetStorageDir();
            var filePath = Path.Combine(dir, hash);

            if (!File.Exists(filePath))
            {
                await File.WriteAllBytesAsync(filePath, data);
                _dbService.RunWrite(realm =>
                {
                    realm.Add(new Paws.Host.Data.Schemas.FileEntry { Hash = hash, Extension = extension });
                });
            }

            return hash;
        }

        public async Task<byte[]?> RetrieveFileAsync(string hash)
        {
            var filePath = Path.Combine(GetStorageDir(), hash);
            if (!File.Exists(filePath)) return null;
            return await File.ReadAllBytesAsync(filePath);
        }
    }
}
