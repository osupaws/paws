using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Paws.Abstractions.Services;

namespace Paws.Core.Services;

public class OsuAuthService : IOsuAuthService
{
    private readonly IConfigService _config;

    private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15)
    })
    {
        DefaultRequestHeaders = { { "User-Agent", "Paws-App" } }
    };

    private TaskCompletionSource<bool>? _callbackTcs;
    private string? _codeVerifier;
    private string? _authState;

    public OsuAuthService(IConfigService config)
    {
        _config = config;
    }

    public string InitiateLogin()
    {
        _codeVerifier = GenerateVerifier();
        var challenge = GenerateChallenge(_codeVerifier);
        _authState = Guid.NewGuid().ToString("N");

        var redirectUri = Uri.EscapeDataString(OsuBuildConfig.RedirectUrl);

        var url = $"{OsuBuildConfig.BaseAuthUrl}/oauth/authorize?client_id={OsuBuildConfig.ClientId}&redirect_uri={redirectUri}&response_type=code&scope=identify&state={_authState}&code_challenge={challenge}&code_challenge_method=S256";

        OpenUrl(url);

        return url;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
    }

    public async Task<bool> WaitForCallbackAsync(int timeoutSeconds = 120)
    {
        _callbackTcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        
        using (cts.Token.Register(() => _callbackTcs.TrySetResult(false)))
        {
            return await _callbackTcs.Task;
        }
    }

    public bool HandleCallback(string url)
    {
        Console.WriteLine($"[OAuth] Received callback URL: {url}");
        try
        {
            string? code = null;
            string? state = null;

            var queryStart = url.IndexOf('?');
            if (queryStart >= 0)
            {
                var queryString = url.Substring(queryStart + 1);
                var pairs = queryString.Split('&');
                foreach (var pair in pairs)
                {
                    var parts = pair.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = Uri.UnescapeDataString(parts[0]);
                        var val = Uri.UnescapeDataString(parts[1]);
                        if (key == "code") code = val;
                        else if (key == "state") state = val;
                    }
                }
            }

            if (state == _authState && !string.IsNullOrEmpty(code))
            {
                Task.Run(async () =>
                {
                    bool success = await ExchangeCodeForTokenAsync(code);
                    _callbackTcs?.TrySetResult(success);
                });
                return true;
            }
            else
            {
                Console.WriteLine($"[OAuth] Validation failed. Expected state: {_authState}, received: {state}");
                _callbackTcs?.TrySetResult(false);
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Exception parsing callback URL: {ex.Message}");
            _callbackTcs?.TrySetResult(false);
            return false;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var expiryStr = await _config.GetSettingAsync("osu_token_expiry");
        var refreshToken = await _config.GetSettingAsync("osu_refresh_token");
        var accessToken = await _config.GetSettingAsync("osu_access_token");

        if (string.IsNullOrEmpty(accessToken)) return null;

        if (long.TryParse(expiryStr, out var expiry) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiry - 60)
        {
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var refreshed = await RefreshTokensAsync(refreshToken);
                if (refreshed)
                {
                    return await _config.GetSettingAsync("osu_access_token");
                }
            }
            return null;
        }

        return accessToken;
    }

    private OsuProfile? _verifiedProfile;

    public async Task LogoutAsync()
    {
        _verifiedProfile = null;
        await _config.SetSettingAsync("osu_access_token", "");
        await _config.SetSettingAsync("osu_refresh_token", "");
        await _config.SetSettingAsync("osu_token_expiry", "");
        await _config.SetSettingAsync("osu_username", "");
        await _config.SetSettingAsync("osu_avatar_url", "");
    }

    private Task<OsuProfile?>? _activeProfileTask;
    private readonly object _taskLock = new object();

    public async Task<OsuProfile?> GetProfileAsync(bool forceRefresh = false)
    {
        Console.WriteLine($"[OAuth] GetProfileAsync: Entering (forceRefresh={forceRefresh})...");

        if (!forceRefresh && _verifiedProfile != null)
        {
            Console.WriteLine($"[OAuth] GetProfileAsync: Returning verified in-memory profile: {_verifiedProfile.Username}");
            return _verifiedProfile;
        }

        Task<OsuProfile?>? currentTask = null;
        lock (_taskLock)
        {
            if (!forceRefresh && _activeProfileTask != null)
            {
                Console.WriteLine("[OAuth] GetProfileAsync: Sharing already active in-flight profile fetch task.");
                currentTask = _activeProfileTask;
            }
            else
            {
                _activeProfileTask = FetchAndVerifyProfileInternalAsync();
                currentTask = _activeProfileTask;
            }
        }

        try
        {
            return await currentTask;
        }
        finally
        {
            lock (_taskLock)
            {
                if (_activeProfileTask == currentTask)
                {
                    _activeProfileTask = null;
                }
            }
        }
    }


    private async Task<OsuProfile?> FetchAndVerifyProfileInternalAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("[OAuth] GetProfileAsync: Token is null or empty.");
            _verifiedProfile = null;
            return null;
        }

        Console.WriteLine($"[OAuth] GetProfileAsync: Verifying connection with {OsuBuildConfig.BaseAuthUrl}/api/v2/me");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{OsuBuildConfig.BaseAuthUrl}/api/v2/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.SendAsync(request);
            Console.WriteLine($"[OAuth] GetProfileAsync: HTTP Status Code = {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<OsuProfile>(json);
                if (profile != null)
                {
                    Console.WriteLine($"[OAuth] GetProfileAsync: Successfully verified profile for user {profile.Username}");
                    await _config.SetSettingAsync("osu_username", profile.Username);
                    await _config.SetSettingAsync("osu_avatar_url", profile.AvatarUrl);
                    _verifiedProfile = profile;
                    return profile;
                }
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("[OAuth] Token is invalid or revoked. Logging out.");
                await LogoutAsync();
            }
            else
            {
                var errContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OAuth] Failed to fetch profile. Code: {response.StatusCode}, Resp: {errContent}");

                // Fallback to cached values if it's a non-auth server/network error
                var cachedUsername = await _config.GetSettingAsync("osu_username");
                var cachedAvatar = await _config.GetSettingAsync("osu_avatar_url");
                if (!string.IsNullOrEmpty(cachedUsername))
                {
                    Console.WriteLine($"[OAuth] Temporary server error. Falling back to DB cache: {cachedUsername}");
                    _verifiedProfile = new OsuProfile { Username = cachedUsername ?? "", AvatarUrl = cachedAvatar ?? "" };
                    return _verifiedProfile;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Exception during profile fetch: {ex.Message}");

            // Fallback to cached values on network/connection timeout errors
            var cachedUsername = await _config.GetSettingAsync("osu_username");
            var cachedAvatar = await _config.GetSettingAsync("osu_avatar_url");
            if (!string.IsNullOrEmpty(cachedUsername))
            {
                Console.WriteLine($"[OAuth] Network connection error. Falling back to DB cache: {cachedUsername}");
                _verifiedProfile = new OsuProfile { Username = cachedUsername ?? "", AvatarUrl = cachedAvatar ?? "" };
                return _verifiedProfile;
            }
        }

        _verifiedProfile = null;
        return null;
    }

    private async Task<bool> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            HttpResponseMessage response;
#if HAS_CLIENT_SECRET
            if (!string.IsNullOrEmpty(OsuBuildConfig.ClientSecret))
            {
                Console.WriteLine("[OAuth] ExchangeCodeForTokenAsync: Exchanging code DIRECTLY with osu! API...");
                var parameters = new Dictionary<string, string>
                {
                    { "client_id", OsuBuildConfig.ClientId },
                    { "client_secret", OsuBuildConfig.ClientSecret },
                    { "code", code },
                    { "grant_type", "authorization_code" },
                    { "redirect_uri", OsuBuildConfig.RedirectUrl },
                    { "code_verifier", _codeVerifier ?? "" }
                };
                var formContent = new FormUrlEncodedContent(parameters);
                response = await _httpClient.PostAsync($"{OsuBuildConfig.BaseAuthUrl}/oauth/token", formContent);
            }
            else
#endif
            {
                Console.WriteLine("[OAuth] ExchangeCodeForTokenAsync: Exchanging code via Proxy Worker...");
                var payload = new
                {
                    code = code,
                    code_verifier = _codeVerifier ?? ""
                };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(OsuBuildConfig.ProxyUrl, jsonContent);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OAuth] Token exchange failed: {error}");
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OsuTokenResponse>(json);
            if (data != null)
            {
                await SaveTokensAsync(data);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Exception during exchange: {ex.Message}");
        }
        return false;
    }

    private async Task<bool> RefreshTokensAsync(string refreshToken)
    {
        try
        {
            HttpResponseMessage response;
#if HAS_CLIENT_SECRET
            if (!string.IsNullOrEmpty(OsuBuildConfig.ClientSecret))
            {
                Console.WriteLine("[OAuth] RefreshTokensAsync: Refreshing tokens DIRECTLY with osu! API...");
                var parameters = new Dictionary<string, string>
                {
                    { "client_id", OsuBuildConfig.ClientId },
                    { "client_secret", OsuBuildConfig.ClientSecret },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                };
                var formContent = new FormUrlEncodedContent(parameters);
                response = await _httpClient.PostAsync($"{OsuBuildConfig.BaseAuthUrl}/oauth/token", formContent);
            }
            else
#endif
            {
                Console.WriteLine("[OAuth] RefreshTokensAsync: Refreshing tokens via Proxy Worker...");
                var payload = new
                {
                    grant_type = "refresh_token",
                    refresh_token = refreshToken
                };
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(OsuBuildConfig.ProxyUrl, jsonContent);
            }

            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OsuTokenResponse>(json);
            if (data != null)
            {
                await SaveTokensAsync(data);
                return true;
            }
        }
        catch { }
        return false;
    }

    private async Task SaveTokensAsync(OsuTokenResponse data)
    {
        await _config.SetSettingAsync("osu_access_token", data.AccessToken);
        await _config.SetSettingAsync("osu_refresh_token", data.RefreshToken);
        var expiryUnix = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn).ToUnixTimeSeconds();
        await _config.SetSettingAsync("osu_token_expiry", expiryUnix.ToString());
    }

    // Zero-allocation cryptographic helpers for PKCE
    private static string GenerateVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string GenerateChallenge(string verifier) => 
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

    private class OsuTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
