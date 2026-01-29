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
                    typeof(PawsConfig),
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

                // --- Migration Logic from PawsConfig (v2) to AppSetting (v3) ---
                var oldConfig = realm.Find<PawsConfig>(0);
                if (oldConfig != null)
                {
                    _logger.LogInformation("Found legacy PawsConfig. Migrating to AppSetting...");
                    realm.Write(() =>
                    {
                        if (oldConfig.StablePath != null) UpsertSettingInternal(realm, "core.paths.stable", oldConfig.StablePath, "string");
                        if (oldConfig.LazerPath != null) UpsertSettingInternal(realm, "core.paths.lazer", oldConfig.LazerPath, "string");
                        UpsertSettingInternal(realm, "core.modes.legacy", oldConfig.IsLegacyMode.ToString().ToLower(), "bool");

                        // Delete legacy config after migration
                        realm.Remove(oldConfig);
                    });
                    _logger.LogInformation("Migration to AppSetting completed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to open Paws database at {path}", _config.DatabasePath);
                throw;
            }
        }

        public RealmConfiguration GetRealmConfiguration() => _config;

        public IEnumerable<ThemeDto> GetAllThemes()
        {
            var results = new List<ThemeDto>();

            using (var realm = Realm.GetInstance(_config))
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
            }

            return results;
        }

        public FileEntry? GetFileEntry(string hash)
        {
            using var realm = Realm.GetInstance(_config);
            var fileEntry = realm.Find<FileEntry>(hash);
            return fileEntry?.Freeze();
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
            return config!.Freeze();
        }

        public void SetConfig(Action<PawsConfig> updateAction)
        {
            using var realm = Realm.GetInstance(_config);
            realm.Write(() =>
            {
                var config = realm.Find<PawsConfig>(0);
                if (config == null)
                {
                    config = realm.Add(new PawsConfig { Id = 0 });
                }
                updateAction(config);
            });
        }

        // --- Key-Value Settings Methods ---

        public IEnumerable<AppSettingDto> GetAllSettings()
        {
            using var realm = Realm.GetInstance(_config);
            return realm.All<AppSetting>().ToList().Select(s => new AppSettingDto(s.Key, s.Value, s.Type)).ToList();
        }

        public AppSettingDto? GetSetting(string key)
        {
            using var realm = Realm.GetInstance(_config);
            var setting = realm.Find<AppSetting>(key);
            return setting != null ? new AppSettingDto(setting.Key, setting.Value, setting.Type) : null;
        }

        public void SetSetting(string key, string value, string type = "string")
        {
            using var realm = Realm.GetInstance(_config);
            realm.Write(() =>
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
