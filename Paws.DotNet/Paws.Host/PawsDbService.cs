using Paws.Host.Data.Schemas;
using Realms;

namespace Paws.Host
{
    /// <summary>
    /// Manages the main Paws application database.
    /// </summary>
    public class PawsDbService
    {
        private readonly ILogger<PawsDbService> _logger;
        private readonly Realm _realm;

        public PawsDbService(ILogger<PawsDbService> logger)
        {
            _logger = logger;
            
            // Define the path for the main application database
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pawsDir = Path.Combine(appData, "Paws");
            Directory.CreateDirectory(pawsDir); // Ensures the directory exists
            var dbPath = Path.Combine(pawsDir, "paws.realm");

            // Define the configuration for our main database
            var config = new RealmConfiguration(dbPath)
            {
                // IsDynamic = false is the default, but we're explicit.
                // We are providing a specific schema.
                IsDynamic = false, 
                Schema = new[]
                {
                    typeof(FileEntry),
                    typeof(Theme)
                },
                SchemaVersion = 1,
            };

            try
            {
                _realm = Realm.GetInstance(config);
                _logger.LogInformation("Paws database opened successfully at {path}", dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to open Paws database at {path}", dbPath);
                // This is a critical failure, rethrow to stop the application
                throw;
            }
        }

        // Method to get all themes from the database
        public IQueryable<Theme> GetAllThemes()
        {
            return _realm.All<Theme>();
        }

        // Add more methods later for adding/updating themes, etc.
    }
}
