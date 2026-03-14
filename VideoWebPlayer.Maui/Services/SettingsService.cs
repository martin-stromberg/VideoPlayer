using Microsoft.Maui.Storage;
using Zeroconf;
using System.Net;
using System.Net.Sockets;

namespace VideoWebPlayer.Maui.Services;

public class SettingsService : ISettingsService
{
    private const string ServerKey = "ServerAddress";
    private const string AuthTokenKey = "AuthToken";

    private const string PlaybackCacheDaysKey = "PlaybackCacheRetentionDays";
    private const string WatchlistCacheDaysKey = "WatchlistCacheRetentionDays";
    private const string DownloadRetentionDaysKey = "DownloadRetentionDays";

    private string GetPrefixedKey(string key)
    {
        var prefix = ProfileManager.Instance.GetPreferencesPrefix();
        return prefix + key;
    }

    public string? ServerAddress => Preferences.Default.Get(GetPrefixedKey(ServerKey), string.Empty);

    public int PlaybackCacheRetentionDays
    {
        get => ClampDays(Preferences.Default.Get(GetPrefixedKey(PlaybackCacheDaysKey), 1));
        set => Preferences.Default.Set(GetPrefixedKey(PlaybackCacheDaysKey), ClampDays(value));
    }

    public int WatchlistCacheRetentionDays
    {
        get => ClampDays(Preferences.Default.Get(GetPrefixedKey(WatchlistCacheDaysKey), 3));
        set => Preferences.Default.Set(GetPrefixedKey(WatchlistCacheDaysKey), ClampDays(value));
    }

    public int DownloadRetentionDays
    {
        get => ClampDays(Preferences.Default.Get(GetPrefixedKey(DownloadRetentionDaysKey), 7));
        set => Preferences.Default.Set(GetPrefixedKey(DownloadRetentionDaysKey), ClampDays(value));
    }

    private static int ClampDays(int days)
    {
        if (days < 1) return 1;
        if (days > 365) return 365;
        return days;
    }

    public bool HasServerAddress()
    {
        var value = Preferences.Default.Get(GetPrefixedKey(ServerKey), string.Empty);
        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetServerAddress(string address)
    {
        Preferences.Default.Set(GetPrefixedKey(ServerKey), address ?? string.Empty);
    }

    public void ClearServerAddress()
    {
        Preferences.Default.Remove(GetPrefixedKey(ServerKey));
    }

    /// <summary>
    /// Gets the authentication token for the current profile.
    /// </summary>
    public string? GetAuthToken()
    {
        return Preferences.Default.Get(GetPrefixedKey(AuthTokenKey), string.Empty);
    }

    /// <summary>
    /// Sets the authentication token for the current profile.
    /// </summary>
    public void SetAuthToken(string token)
    {
        Preferences.Default.Set(GetPrefixedKey(AuthTokenKey), token ?? string.Empty);
    }

    /// <summary>
    /// Clears the authentication token for the current profile.
    /// </summary>
    public void ClearAuthToken()
    {
        Preferences.Default.Remove(GetPrefixedKey(AuthTokenKey));
    }

    // mDNS Discovery (Zeroconf)
    public async Task<IReadOnlyList<string>> DiscoverServersAsync(int timeoutMs = 3000)
    {
        var found = new List<string>();
        try
        {
            var responses = await ZeroconfResolver.ResolveAsync("_http._tcp.local.", TimeSpan.FromMilliseconds(timeoutMs));
            foreach (var resp in responses)
            {
                foreach (var svc in resp.Services.Values)
                {
                    var ip = resp.IPAddress;
                    var port = svc.Port;
                    found.Add($"http://{ip}:{port}");
                }
            }
        }
        catch { }
        return found;
    }

    // UDP Broadcast Discovery (Fallback)
    public async Task<IReadOnlyList<string>> DiscoverServersUdpAsync(int port = 5001, int timeoutMs = 2000)
    {
        var found = new List<string>();
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        var request = System.Text.Encoding.UTF8.GetBytes("VIDEOWEBPLAYER_DISCOVERY");
        var broadcastAddr = new IPEndPoint(IPAddress.Broadcast, port);
        await client.SendAsync(request, request.Length, broadcastAddr);

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (client.Available > 0)
            {
                var result = await client.ReceiveAsync();
                var response = System.Text.Encoding.UTF8.GetString(result.Buffer);
                if (response.StartsWith("VIDEOWEBPLAYER_SERVER:"))
                {
                    found.Add(response.Substring("VIDEOWEBPLAYER_SERVER:".Length));
                }
            }
            await Task.Delay(100);
        }
        return found;
    }
}
