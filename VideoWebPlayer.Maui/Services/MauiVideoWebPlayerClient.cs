using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// MAUI-spezifische VideoWebPlayerClient, die Token-Verwaltung mit Preferences übernimmt.
/// </summary>
public class MauiVideoWebPlayerClient : VideoWebPlayerClient
{
    private const string AuthTokenKey = "AuthToken";
    private readonly ILogger<MauiVideoWebPlayerClient> _logger;

    public MauiVideoWebPlayerClient(HttpClient httpClient, ILogger<MauiVideoWebPlayerClient> logger) 
        : base(httpClient, logger)
    {
        _logger = logger;
        
        // Lade den gespeicherten Token beim Starten
        LoadAuthTokenFromPreferences();
    }

    /// <summary>
    /// Lädt den gespeicherten Authorization Token aus Preferences und setzt ihn im HttpClient.
    /// </summary>
    private void LoadAuthTokenFromPreferences()
    {
        try
        {
            var token = Preferences.Default.Get(AuthTokenKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(token))
            {
                SetAuthorizationToken(new AuthorizationToken { token = token });
                _logger?.LogInformation("Authorization Token aus Preferences geladen.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Laden des Authorization Token aus Preferences.");
        }
    }
    
    /// <summary>
    /// Lädt den Token neu aus Preferences (z.B. nach Login).
    /// </summary>
    public void ReloadAuthToken()
    {
        LoadAuthTokenFromPreferences();
    }

    /// <summary>
    /// Überschreibt SetAuthorizationToken, um den Token auch in Preferences zu speichern.
    /// </summary>
    public override void SetAuthorizationToken(AuthorizationToken token)
    {
        base.SetAuthorizationToken(token);
        
        if (token is not null)
        {
            try
            {
                Preferences.Default.Set(AuthTokenKey, token.token);
                _logger?.LogInformation("Authorization Token in Preferences gespeichert.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Fehler beim Speichern des Authorization Token in Preferences.");
            }
        }
    }

    /// <summary>
    /// Löscht den gespeicherten Authorization Token (z.B. bei Logout).
    /// </summary>
    public void ClearAuthorizationToken()
    {
        try
        {
            Preferences.Default.Remove(AuthTokenKey);
            SetAuthorizationToken(new AuthorizationToken { token = string.Empty });
            _logger?.LogInformation("Authorization Token gelöscht.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Löschen des Authorization Token.");
        }
    }
}
