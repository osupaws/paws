using System.Security.Cryptography;

namespace Paws.Host
{
    public class FileStorageService
    {
        private readonly string _baseDir;

        public FileStorageService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pawsDir = Path.Combine(appData, "Paws");
            _baseDir = Path.Combine(pawsDir, "files");
            Directory.CreateDirectory(_baseDir);
        }

        /// <summary>
        /// Calculates the SHA-256 hash of a given byte array.
        /// </summary>
        public string CalculateHash(byte[] buffer)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(buffer);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        
        /// <summary>
        /// Gets the full path to a file based on its hash.
        /// </summary>
        private string GetHashedFilePath(string hash)
        {
            // All files will be stored directly in the 'files' directory.
            return Path.Combine(_baseDir, hash);
        }

        /// <summary>
        /// Stores a file's content on disk using a hash-based hierarchical structure.
        /// </summary>
        /// <returns>The hash of the stored file.</returns>
        public async Task<string> StoreFileAsync(byte[] buffer)
        {
            var hash = CalculateHash(buffer);
            var filePath = GetHashedFilePath(hash);

            if (!File.Exists(filePath))
            {
                await File.WriteAllBytesAsync(filePath, buffer);
            }

            return hash;
        }

        /// <summary>
        /// Retrieves a file's content from disk based on its hash.
        /// </summary>
        /// <returns>The file content as a byte array, or null if not found.</returns>
        public async Task<byte[]?> RetrieveFileAsync(string hash)
        {
            var filePath = GetHashedFilePath(hash);
            if (File.Exists(filePath))
            {
                return await File.ReadAllBytesAsync(filePath);
            }
            return null;
        }
    }
}
