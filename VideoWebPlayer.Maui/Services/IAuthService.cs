namespace VideoWebPlayer.Maui.Services;

public interface IAuthService
{
    bool HasCredentials();
    (string username, string password) GetCredentials();
    /// <summary>
    /// Attempts to login and returns a tuple(success, errorMessage). On success errorMessage is null.
    /// </summary>
    Task<(bool success, string? errorMessage)> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    void ClearCredentials();
}
