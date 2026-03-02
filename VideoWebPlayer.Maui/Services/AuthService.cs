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
        var username = Preferences.Default.Get(UserKey, string.Empty);
        var password = Preferences.Default.Get(PasswordKey, string.Empty);
        return !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
    }
    
    public (string username, string password) GetCredentials()
    {
        var username = Preferences.Default.Get(UserKey, string.Empty);
        var password = Preferences.Default.Get(PasswordKey, string.Empty);
        return (username, password);
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
            // Verwende den DI-registrierten MauiVideoWebPlayerClient
            var client = App.ServiceProvider?.GetService<VideoWebPlayerClient>();
            if (client == null)
            {
                // Fallback: Erstelle temporären Client für Login
                var httpClient = new HttpClient { BaseAddress = new Uri(serverAddress) };
                httpClient.DefaultRequestHeaders.Add("X-API-Key", ApiToken);
                var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoWebPlayerClient>();
                client = new VideoWebPlayerClient(httpClient, logger);
            }
            
            var token = await client.AuthenticateAsync(username, password);
            if (token != null && !string.IsNullOrWhiteSpace(token.token))
            {
                Preferences.Default.Set(TokenKey, token.token);
                
                // Lade Token neu in den DI-Client
                if (client is MauiVideoWebPlayerClient mauiClient)
                {
                    mauiClient.ReloadAuthToken();
                }
                
                SaveCredentials(username, password);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login failed: {ex.Message}");
        }
        return false;
    }
}
