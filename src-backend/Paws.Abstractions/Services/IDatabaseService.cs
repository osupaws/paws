using Realms;

namespace Paws.Abstractions.Services;

public interface IDatabaseService
{
    string DatabasePath { get; }
    string DataDirectory { get; }
    string PluginsDirectory { get; }
    string TempDirectory { get; }

    Realm GetRealm();
}
