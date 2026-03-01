using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.Services;

public class AuthService : IAuthService
{
    private const string UserKey = "Username";
    private const string PasswordKey = "Password";
    private const string TokenKey = "AuthToken";
    
    private const string ApiToken = "00saHJj4IrjWNUytUZDUwXHqq6EiCKMJPyKh9c6hykPT3NyS3d2CVUkb8E8TMWQWJ7y6sOSpC";

    public bool HasCredentials()
    {
        var u = Preferences.Default.Get(UserKey, string.Empty);
        var p = Preferences.Default.Get(PasswordKey, string.Empty);
        return !string.IsNullOrWhiteSpace(u) && !string.IsNullOrWhiteSpace(p);
    }

    public void SaveCredentials(string username, string password)
    {
        Preferences.Default.Set(UserKey, username ?? string.Empty);
        Preferences.Default.Set(PasswordKey, password ?? string.Empty);
    }

    public void ClearCredentials()
    {
        Preferences.Default.Remove(UserKey);
        Preferences.Default.Remove(PasswordKey);
        Preferences.Default.Remove(TokenKey);
    }

    public async Task<bool> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var serverAddress = Preferences.Default.Get("ServerAddress", string.Empty);
        if (string.IsNullOrWhiteSpace(serverAddress))
            return false;

        try
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(serverAddress) };
            httpClient.DefaultRequestHeaders.Add("X-API-Key", ApiToken);
            
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoWebPlayerClient>();
            
            var client = new VideoWebPlayerClient(httpClient, logger);
            
            var token = await client.AuthenticateAsync(username, password);
            if (token != null && !string.IsNullOrWhiteSpace(token.token))
            {
                Preferences.Default.Set(TokenKey, token.token);
                SaveCredentials(username, password);
                return true;
            }
        }
        catch
        {
            // ignore, login failed
        }
        return false;
    }
}
