using Paws.Host.Data;
using Paws.Host.Data.Schemas;
using Realms;
using System.Collections.Generic;
using System.Linq;

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
                    typeof(AppSetting),
                    typeof(FileBlob),
                    typeof(Plugin),
                    typeof(PluginFile)
                },
                SchemaVersion = 7,
            };
        }

        public async Task InitializeAsync()
        {
            try
            {
                using var realm = await Realm.GetInstanceAsync(_config);
                _logger.LogInformation("Paws database configuration validated successfully at {path}", _config.DatabasePath);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to open Paws database at {path}", _config.DatabasePath);
                throw;
            }
        }

        public RealmConfiguration GetRealmConfiguration() => _config;

        public void RunRead(Action<Realm> action)
        {
            using var realm = Realm.GetInstance(_config);
            action(realm);
        }

        public T RunRead<T>(Func<Realm, T> action)
        {
            using var realm = Realm.GetInstance(_config);
            return action(realm);
        }

        public void RunWrite(Action<Realm> action)
        {
            using var realm = Realm.GetInstance(_config);
            realm.Write(() => action(realm));
        }

        public async Task RunWriteAsync(Action<Realm> action)
        {
             using var realm = Realm.GetInstance(_config);
             await realm.WriteAsync(() => action(realm));
        }

        public IEnumerable<ThemeDto> GetAllThemes()
        {
            var results = new List<ThemeDto>();

            RunRead(realm =>
            {
                var allThemes = realm.All<Theme>();

                foreach (var theme in allThemes)
                {
                    var fileDto = theme.File == null
                        ? null
                        : new FileEntryDto(theme.File.Hash, theme.File.Size, theme.File.Extension);

                    var themeDto = new ThemeDto(
                        theme.Id,
                        theme.Name,
                        theme.Base,
                        theme.Author,
                        theme.Version,
                        fileDto
                    );

                    results.Add(themeDto);
                }
            });

            return results;
        }

        public FileEntry? GetFileEntry(string hash)
        {
            return RunRead(realm =>
            {
                var fileEntry = realm.Find<FileEntry>(hash);
                return fileEntry?.Freeze();
            });
        }

        // --- Key-Value Settings Methods ---

        public IEnumerable<AppSettingDto> GetAllSettings()
        {
            return RunRead(realm => realm.All<AppSetting>().ToList().Select(s => new AppSettingDto(s.Key, s.Value, s.Type)).ToList());
        }

        public AppSettingDto? GetSetting(string key)
        {
            return RunRead(realm =>
            {
                var setting = realm.Find<AppSetting>(key);
                return setting != null ? new AppSettingDto(setting.Key, setting.Value, setting.Type) : null;
            });
        }

        public void SetSetting(string key, string value, string type = "string")
        {
            RunWrite(realm =>
            {
                UpsertSettingInternal(realm, key, value, type);
            });
        }

        private void UpsertSettingInternal(Realm realm, string key, string value, string type)
        {
            var setting = realm.Find<AppSetting>(key);
            if (setting == null)
            {
                realm.Add(new AppSetting { Key = key, Value = value, Type = type });
            }
            else
            {
                setting.Value = value;
                setting.Type = type;
            }
        }
    }
}
