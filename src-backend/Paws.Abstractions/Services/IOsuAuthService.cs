using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace Paws.Abstractions.Services;

public class OsuProfile
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;
}

public interface IOsuAuthService
{
    string InitiateLogin();
    Task<bool> WaitForCallbackAsync(int timeoutSeconds = 120);
    bool HandleCallback(string url);
    Task<string?> GetAccessTokenAsync();
    Task<OsuProfile?> GetProfileAsync(bool forceRefresh = false);
    Task LogoutAsync();
}
