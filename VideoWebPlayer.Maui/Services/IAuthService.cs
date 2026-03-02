namespace VideoWebPlayer.Maui.Services;

public interface IAuthService
{
    bool HasCredentials();
    (string username, string password) GetCredentials();
    Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    void ClearCredentials();
}
