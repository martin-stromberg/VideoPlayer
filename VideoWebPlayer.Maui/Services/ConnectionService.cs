using System.Net.Http;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Maui.Services;

public class ConnectionService : IConnectionService
{
    private const string TokenKey = "AuthToken";
    
    // API-Token für MAUI (Sicherheit kommt von Benutzer-Authentifizierung)
    private const string ApiToken = "00saHJj4IrjWNUytUZDUwXHqq6EiCKMJPyKh9c6hykPT3NyS3d2CVUkb8E8TMWQWJ7y6sOSpC";

    public ConnectionService()
    {
    }

    public async Task<bool> TryConnectAsync(string baseAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
            return false;

        try
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
            
            // Versuche JWT Token zu laden (wenn der Benutzer angemeldet ist)
            var jwtToken = Preferences.Default.Get(TokenKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(jwtToken))
            {
                // Verwende Bearer Token für authentifizierte Requests
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
            }
            else
            {
                // Fallback: Verwende hardkodierten API-Token
                httpClient.DefaultRequestHeaders.Add("X-API-Key", ApiToken);
            }
            
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoWebPlayerClient>();
            
            var client = new VideoWebPlayerClient(httpClient, logger);
            return await client.HealthCheckAsync();
        }
        catch
        {
            return false;
        }
    }
}
