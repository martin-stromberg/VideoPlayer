using Microsoft.Extensions.Logging;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// MAUI-spezifische VideoWebPlayerClient, die Token-Verwaltung mit Preferences übernimmt.
/// </summary>
    public class MauiVideoWebPlayerClient : VideoWebPlayerClient
    {
        private readonly ILogger<MauiVideoWebPlayerClient> _logger;
    private readonly IAuthService _authService;
    private readonly ISettingsService _settings;

        public MauiVideoWebPlayerClient(HttpClient httpClient, ISettingsService settings, ILogger<MauiVideoWebPlayerClient> logger, IAuthService authService)
            : base(httpClient, logger)
        {
            _logger = logger;
        _authService = authService;
        _settings = settings;

            // Lade den gespeicherten Token beim Starten
            LoadAuthTokenFromSettings();
        }

    protected override async Task<bool> HandleUnauthorized()
    {
        var handled = await base.HandleUnauthorized();
        // Reagiere auf Unauthorized-Ereignisse für diese Client-Instanz
        // und versuche bei gespeicherten Credentials eine stille Anmeldung mit diesem Client.
        if (_authService is null)
            return false;
        if (!_authService.HasCredentials())
            return false;
        try
        {
            var (user, pass) = _authService.GetCredentials();
            // Versuche, mit diesem Client direkt zu authentifizieren und
            // setze anschließend den neuen Token auf dieser Instanz.
            var token = await AuthenticateAsync(user, pass);
            if (token is not null && !string.IsNullOrWhiteSpace(token.token))
            {
                SetAuthorizationToken(token);
                _logger?.LogInformation("Silent re-login succeeded and token applied to MauiVideoWebPlayerClient.");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Silent re-login failed.");
        }
        return false;
    }

    /// <summary>
    /// Lädt den gespeicherten Authorization Token aus Preferences und setzt ihn im HttpClient.
    /// </summary>
    private void LoadAuthTokenFromSettings()
    {
        try
        {
            var token = _settings.GetAuthToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                SetAuthorizationToken(new AuthorizationToken { token = token });
				// Intentionally no persistence here (SetAuthorizationToken is overridden below).
                _logger?.LogInformation("Authorization Token aus Settings geladen.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Laden des Authorization Token aus Settings.");
        }
    }
    
    /// <summary>
    /// Lädt den Token neu aus Preferences (z.B. nach Login).
    /// </summary>
    public void ReloadAuthToken()
    {
        LoadAuthTokenFromSettings();
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
				_settings.SetAuthToken(token.token);
				_logger?.LogInformation("Authorization Token in Settings gespeichert.");
            }
            catch (Exception ex)
            {
				_logger?.LogWarning(ex, "Fehler beim Speichern des Authorization Token in Settings.");
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
            _settings.ClearAuthToken();
            SetAuthorizationToken(new AuthorizationToken { token = string.Empty });
            _logger?.LogInformation("Authorization Token gelöscht.");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fehler beim Löschen des Authorization Token.");
        }
    }
}
