using Paws.Host.Data.Schemas;
using Realms;

namespace Paws.Host
{
    public class PawsDbService
    {
        private readonly ILogger<PawsDbService> _logger;
        private readonly RealmConfiguration _config;

        public PawsDbService(ILogger<PawsDbService> logger)
        {
            _logger = logger;
            
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pawsDir = Path.Combine(appData, "Paws");
            Directory.CreateDirectory(pawsDir);
            var dbPath = Path.Combine(pawsDir, "paws.realm");

            _config = new RealmConfiguration(dbPath)
            {
                IsDynamic = false,
                Schema = new[]
                {
                    typeof(FileEntry),
                    typeof(Theme),
                    typeof(PawsConfig)
                },
                SchemaVersion = 1,
            };
        }

        // This method can be used on startup to validate the config and ensure the DB can be opened.
        public async Task InitializeAsync()
        {
            try
            {
                // Open and immediately close a Realm instance to check for errors.
                using var realm = await Realm.GetInstanceAsync(_config);
                _logger.LogInformation("Paws database configuration validated successfully at {path}", _config.DatabasePath);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to open Paws database at {path}", _config.DatabasePath);
                throw;
            }
        }
        
        public IQueryable<Theme> GetAllThemes()
        {
            using var realm = Realm.GetInstance(_config);
            // We must convert the live Realm results to a list in memory
            // to be able to use it outside of the realm's thread/context.
            return realm.All<Theme>().ToList().AsQueryable();
        }

        public PawsConfig GetConfig()
        {
            using var realm = Realm.GetInstance(_config);
            var config = realm.Find<PawsConfig>(0);
            if (config == null)
            {
                realm.Write(() =>
                {
                    config = realm.Add(new PawsConfig { Id = 0 });
                });
            }
            // Freeze the object to make it thread-safe and returnable.
            return config!.Freeze();
        }

        public void SetConfig(Action<PawsConfig> updateAction)
        {
            using var realm = Realm.GetInstance(_config);
            realm.Write(() =>
            {
                // We must find the object again within this write transaction context.
                var config = realm.Find<PawsConfig>(0);
                if (config == null)
                {
                    config = realm.Add(new PawsConfig { Id = 0 });
                }
                updateAction(config);
            });
        }
    }
}
