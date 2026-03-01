using System.Net.Http.Headers;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// HttpClientHandler für Bild-Requests mit Bearer Token.
/// Wird als UriImageSource Handler registriert, um Bearer Token zu Bild-Requests hinzuzufügen.
/// </summary>
public class AuthorizedImageHttpClientHandler : HttpClientHandler
{
    private readonly string _bearerToken;

    public AuthorizedImageHttpClientHandler(string bearerToken)
    {
        _bearerToken = bearerToken;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Füge Bearer Token zu jedem Request hinzu
        if (!string.IsNullOrWhiteSpace(_bearerToken) && 
            !request.Headers.Contains("Authorization"))
        {
            request.Headers.Authorization = 
                new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
