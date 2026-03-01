namespace VideoWebPlayer.Maui.Services;

public interface IAuthService
{
    bool HasCredentials();
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    void SaveCredentials(string username, string password);
    void ClearCredentials();
}
