using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Paws.Abstractions.Models;
using Paws.Abstractions.Services;

namespace Paws.Core.Rpc;

/// <summary>
/// Handles game-related RPC commands (beatmaps, scores, client status).
/// </summary>
public class GameHandler : IRpcHandler
{
    private readonly IGameDataService _gameData;

    public GameHandler(IGameDataService gameData)
    {
        _gameData = gameData;
    }

    public bool CanHandle(string action) => 
        action.StartsWith("game") || 
        action == "getBeatmapSets" || 
        action == "getCollections" ||
        action == "getScores" ||
        action == "getAllSkins" ||
        action == "searchBeatmaps";

    public async Task<object?> HandleAsync(string action, string callerId, Dictionary<string, JsonElement> parameters)
    {
        switch (action)
        {
            case "getBeatmapSets":
            case "game:active:getBeatmapSets":
                return await _gameData.GetAllBeatmapSetsAsync();

            case "searchBeatmaps":
            case "game:active:searchBeatmaps":
                if (parameters.TryGetValue("query", out var queryEl))
                {
                    return await _gameData.SearchBeatmapsAsync(queryEl.GetString() ?? "");
                }
                throw new ArgumentException("Missing 'query' parameter");

            case "game:active:getCollections":
                return await _gameData.GetAllCollectionsAsync();

            case "game:active:getScores":
                if (parameters.TryGetValue("hash", out var hashEl))
                {
                    return await _gameData.GetScoresByBeatmapHashAsync(hashEl.GetString() ?? "");
                }
                throw new ArgumentException("Missing 'hash' parameter");

            case "game:active:getAllSkins":
                return await _gameData.GetAllSkinsAsync();

            case "game:stable:getBeatmapSets":
                // TODO: Direct driver access
                return await _gameData.GetAllBeatmapSetsAsync(); 

            case "game:active:deleteRecord":
                if (parameters.TryGetValue("type", out var delTypeEl) && parameters.TryGetValue("id", out var delIdEl))
                {
                    var client = _gameData.GetActiveClient();
                    return await _gameData.DeleteRecordAsync(callerId, client, delTypeEl.GetString() ?? "", delIdEl.GetString() ?? "");
                }
                throw new ArgumentException("Missing 'type' or 'id'");

            case "game:active:updateRecord":
                if (parameters.TryGetValue("type", out var upTypeEl) && parameters.TryGetValue("id", out var upIdEl) && parameters.TryGetValue("data", out var upDataEl))
                {
                    var client = _gameData.GetActiveClient();
                    return await _gameData.UpdateRecordAsync(callerId, client, upTypeEl.GetString() ?? "", upIdEl.GetString() ?? "", upDataEl);
                }
                throw new ArgumentException("Missing 'type', 'id' or 'data'");

            default:
                throw new NotSupportedException($"Action {action} not supported by GameHandler");
        }
    }

    public string? GetRequiredScope(string action) => "game:data:read";

    public bool IsHostOnly(string action) => false;
}
