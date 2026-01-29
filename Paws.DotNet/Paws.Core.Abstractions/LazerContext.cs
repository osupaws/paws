using Realms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Paws.Core.Abstractions;

/// <summary>
/// A disposable context for accessing osu!lazer data in a decoupled, strongly-typed manner.
/// usage: using var context = host.GetLazerContext();
/// </summary>
public class LazerContext : IDisposable
{
    private readonly Realm _realm;

    public LazerContext(object? realmInstance)
    {
        // We take object? to avoid forcing Realm reference on all consumers immediately,
        // but internal implementation knows it's a Realm.
        if (realmInstance is Realm r)
        {
            _realm = r;
        }
        else
        {
            throw new ArgumentException("LazerContext requires a valid Realm instance.", nameof(realmInstance));
        }
    }

    public void Dispose()
    {
        _realm?.Dispose();
    }

    // --- Accessors ---

    public IEnumerable<LazerBeatmapSet> BeatmapSets
        => _realm.DynamicApi.All(LazerSchema.BeatmapSet).AsEnumerable().Select(x => new LazerBeatmapSet(x));

    public IEnumerable<LazerBeatmap> Beatmaps
        => _realm.DynamicApi.All(LazerSchema.Beatmap).AsEnumerable().Select(x => new LazerBeatmap(x));

    public IEnumerable<LazerRuleset> Rulesets
        => _realm.DynamicApi.All(LazerSchema.Ruleset).AsEnumerable().Select(x => new LazerRuleset(x));

    public IEnumerable<LazerRealmFile> Files
        => _realm.DynamicApi.All(LazerSchema.File).AsEnumerable().Select(x => new LazerRealmFile(x));
}
