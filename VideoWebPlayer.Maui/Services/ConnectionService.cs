using System.Net.Http;
using Microsoft.Maui.Storage;
using VideoWebPlayer.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

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
        // Normalize address (ensure scheme is present)
        var addr = baseAddress.Trim();
        if (!addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            addr = "http://" + addr;

        try
        {
            // Create HttpClient with a short timeout so the UI doesn't hang for long when server is unreachable
            var httpClient = new HttpClient { BaseAddress = new Uri(addr), Timeout = TimeSpan.FromSeconds(8) };

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

            System.Diagnostics.Debug.WriteLine($"[ConnectionService] Trying healthcheck against {addr} with timeout {httpClient.Timeout.TotalSeconds}s");

            var result = await client.HealthCheckAsync();
            System.Diagnostics.Debug.WriteLine($"[ConnectionService] HealthCheck result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionService] TryConnectAsync failed: {ex.Message}");
            return false;
        }

    }

    public async Task<ConnectionState> StartConnectionWorkflowAsync(ViewModels.HomePageViewModel viewModel, IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Show connecting indicator
        try
        {
            viewModel.IsLoading = true;

            var settings = services.GetService<ISettingsService>();
            var auth = services.GetService<IAuthService>();

            // Ensure server address
            if (settings == null || string.IsNullOrWhiteSpace(settings.ServerAddress))
            {
                viewModel.IsOfflineMode = true;
                return ConnectionState.NeedsServerSetup;
            }

            var connected = await TryConnectAsync(settings.ServerAddress!, cancellationToken);
            if (!connected)
            {
                viewModel.IsOfflineMode = true;
                return ConnectionState.Offline;
            }

            // Connected - check credentials
            if (auth == null || !auth.HasCredentials())
            {
                viewModel.IsOfflineMode = true;
                return ConnectionState.NeedsLogin;
            }

            // attempt login using saved creds
            var creds = auth.GetCredentials();
            var (success, _) = await auth.LoginAsync(creds.username, creds.password, cancellationToken);
            if (!success)
            {
                viewModel.IsOfflineMode = true;
                return ConnectionState.NeedsLogin;
            }

            // success
            viewModel.IsOfflineMode = false;
            return ConnectionState.Connected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConnectionService] StartConnectionWorkflowAsync failed: {ex.Message}");
            viewModel.IsOfflineMode = true;
            return ConnectionState.Offline;
        }
        finally
        {
            viewModel.IsLoading = false;
        }
    }
}
