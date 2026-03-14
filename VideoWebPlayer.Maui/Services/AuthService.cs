using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.Services;

public class AuthService : IAuthService
{
    private readonly ISettingsService _settings;

    private const string UserKey = "Username";
    private const string PasswordKey = "Passkey";
    private const string TokenKey = "AuthToken";
    
    private const string ApiToken = "00saHJj4IrjWNUytUZDUwXHqq6EiCKMJPyKh9c6hykPT3NyS3d2CVUkb8E8TMWQWJ7y6sOSpC";

	public AuthService() : this(new SettingsService())
	{
	}

	public AuthService(ISettingsService settings)
	{
		_settings = settings;
	}

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
		_settings.ClearAuthToken();
    }

    public async Task<(bool success, string? errorMessage)> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var serverAddress = _settings.ServerAddress;
        if (string.IsNullOrWhiteSpace(serverAddress))
            return (false, "Keine Serveradresse gesetzt. Bitte Server in den Einstellungen angeben.");

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
                _settings.SetAuthToken(token.token);

                // Lade Token neu in den DI-Client
                if (client is MauiVideoWebPlayerClient mauiClient)
                {
                    mauiClient.ReloadAuthToken();
                }

                SaveCredentials(username, password);
                return (true, null);
            }
            else
            {
                // Authentication failed but no exception -> invalid credentials
                return (false, "Ungültiger Benutzername oder Passwort.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login failed: {ex.Message}");
            return (false, $"Fehler bei der Anmeldung: {ex.Message}");
        }
    }
}
