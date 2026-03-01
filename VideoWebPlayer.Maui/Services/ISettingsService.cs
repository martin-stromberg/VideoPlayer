namespace VideoWebPlayer.Maui.Services;

public interface ISettingsService
{
    string? ServerAddress { get; }
    bool HasServerAddress();
    void SetServerAddress(string address);
    void ClearServerAddress();

    // Discovery methods
    Task<IReadOnlyList<string>> DiscoverServersAsync(int timeoutMs = 3000);
    Task<IReadOnlyList<string>> DiscoverServersUdpAsync(int port = 5001, int timeoutMs = 2000);
}
