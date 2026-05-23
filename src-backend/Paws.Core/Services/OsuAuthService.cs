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

    private const string ClientId = "41";
    private const string BaseAuthUrl = "https://dev.ppy.sh";
    private const string ProxyUrl = "https://paws-auth.shshx.workers.dev/";

    private HttpListener? _listener;
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

        var redirectUri = Uri.EscapeDataString("http://127.0.0.1:40012/callback");

        StartListener();

        var url = $"{BaseAuthUrl}/oauth/authorize?client_id={ClientId}&redirect_uri={redirectUri}&response_type=code&scope=identify&state={_authState}&code_challenge={challenge}&code_challenge_method=S256";

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
        if (_listener == null || !_listener.IsListening) return false;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // Asynchronously wait for the browser redirect context
            var getContextTask = _listener.GetContextAsync();
            var completedTask = await Task.WhenAny(getContextTask, Task.Delay(-1, cts.Token));

            if (completedTask != getContextTask)
            {
                // Timeout hit
                return false;
            }

            var context = await getContextTask;
            var req = context.Request;
            var res = context.Response;

            var code = req.QueryString["code"];
            var state = req.QueryString["state"];

            bool success = false;
            if (state == _authState && !string.IsNullOrEmpty(code))
            {
                success = await ExchangeCodeForTokenAsync(code);
            }

            string responseHtml = success
                ? "<html><head><meta charset='utf-8'/><style>body{font-family:sans-serif;text-align:center;padding:50px;background:#181213;color:#eddfe1;}</style></head><body><h1>Paws Connection Successful!</h1><p>You can close this tab and return to the app.</p></body></html>"
                : "<html><head><meta charset='utf-8'/><style>body{font-family:sans-serif;text-align:center;padding:50px;background:#181213;color:#ffb4ab;}</style></head><body><h1>Connection Failed</h1><p>State mismatch or invalid authorization code.</p></body></html>";

            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            res.ContentLength64 = buffer.Length;
            res.ContentType = "text/html; charset=utf-8";
            await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            res.OutputStream.Close();

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Error processing callback: {ex.Message}");
            return false;
        }
        finally
        {
            StopListener();
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

    public async Task LogoutAsync()
    {
        await _config.SetSettingAsync("osu_access_token", "");
        await _config.SetSettingAsync("osu_refresh_token", "");
        await _config.SetSettingAsync("osu_token_expiry", "");
        await _config.SetSettingAsync("osu_username", "");
        await _config.SetSettingAsync("osu_avatar_url", "");
    }

    public async Task<OsuProfile?> GetProfileAsync(bool forceRefresh = false)
    {
        Console.WriteLine($"[OAuth] GetProfileAsync: Entering (forceRefresh={forceRefresh})...");
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("[OAuth] GetProfileAsync: Token is null or empty.");
            return null;
        }

        var cachedUsername = await _config.GetSettingAsync("osu_username");
        var cachedAvatar = await _config.GetSettingAsync("osu_avatar_url");

        if (!forceRefresh && !string.IsNullOrEmpty(cachedUsername) && !string.IsNullOrEmpty(cachedAvatar))
        {
            Console.WriteLine($"[OAuth] GetProfileAsync: Found cached profile: {cachedUsername}");
            return new OsuProfile { Username = cachedUsername, AvatarUrl = cachedAvatar };
        }

        Console.WriteLine($"[OAuth] GetProfileAsync: No cache. Fetching from {BaseAuthUrl}/api/v2/me");
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Paws-App");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await client.GetAsync($"{BaseAuthUrl}/api/v2/me");
            Console.WriteLine($"[OAuth] GetProfileAsync: HTTP Status Code = {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var profile = JsonSerializer.Deserialize<OsuProfile>(json);
                if (profile != null)
                {
                    Console.WriteLine($"[OAuth] GetProfileAsync: Successfully fetched profile for user {profile.Username}");
                    await _config.SetSettingAsync("osu_username", profile.Username);
                    await _config.SetSettingAsync("osu_avatar_url", profile.AvatarUrl);
                    return profile;
                }
            }
            else
            {
                var errContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[OAuth] Failed to fetch profile. Code: {response.StatusCode}, Resp: {errContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Exception during profile fetch: {ex.Message}");
        }

        return null;
    }

    private void StartListener()
    {
        StopListener();

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:40012/callback/");
            _listener.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OAuth] Failed to start HttpListener: {ex}");
            throw new InvalidOperationException($"Failed to bind port 40012. Details: {ex.Message}", ex);
        }
    }

    private void StopListener()
    {
        if (_listener == null) return;
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
        }
        catch { }
        _listener = null;
    }

    private async Task<bool> ExchangeCodeForTokenAsync(string code)
    {
        using var client = new HttpClient();
        var payload = new
        {
            code = code,
            code_verifier = _codeVerifier ?? ""
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            var response = await client.PostAsync(ProxyUrl, jsonContent);
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
                await _config.SetSettingAsync("osu_access_token", data.AccessToken);
                await _config.SetSettingAsync("osu_refresh_token", data.RefreshToken);

                var expiryUnix = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn).ToUnixTimeSeconds();
                await _config.SetSettingAsync("osu_token_expiry", expiryUnix.ToString());
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
        using var client = new HttpClient();
        var payload = new
        {
            grant_type = "refresh_token",
            refresh_token = refreshToken
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        try
        {
            var response = await client.PostAsync(ProxyUrl, jsonContent);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<OsuTokenResponse>(json);
            if (data != null)
            {
                await _config.SetSettingAsync("osu_access_token", data.AccessToken);
                await _config.SetSettingAsync("osu_refresh_token", data.RefreshToken);

                var expiryUnix = DateTimeOffset.UtcNow.AddSeconds(data.ExpiresIn).ToUnixTimeSeconds();
                await _config.SetSettingAsync("osu_token_expiry", expiryUnix.ToString());
                return true;
            }
        }
        catch { }
        return false;
    }

    // Cryptographic Helpers for PKCE
    private static string GenerateVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateChallenge(string verifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

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
