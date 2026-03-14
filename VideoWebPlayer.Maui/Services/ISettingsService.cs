namespace VideoWebPlayer.Maui.Services;

public interface ISettingsService
{
    string? ServerAddress { get; }
    bool HasServerAddress();
    void SetServerAddress(string address);
    void ClearServerAddress();

	string? GetAuthToken();
	void SetAuthToken(string token);
	void ClearAuthToken();

    int PlaybackCacheRetentionDays { get; set; }
    int WatchlistCacheRetentionDays { get; set; }
    int DownloadRetentionDays { get; set; }

    // Discovery methods
    Task<IReadOnlyList<string>> DiscoverServersAsync(int timeoutMs = 3000);
    Task<IReadOnlyList<string>> DiscoverServersUdpAsync(int port = 5001, int timeoutMs = 2000);
}
