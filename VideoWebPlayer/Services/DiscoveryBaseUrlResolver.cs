using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves the public base URL reported in UDP discovery answers, once per request:
/// reads the admin-maintained override (<c>Setup.DiscoveryPublicBaseUrl</c>) in a fresh
/// scope, collects the bound server addresses and the host's DNS identity and hands
/// everything to <see cref="DiscoveryResponseBuilder"/>. Per-request resolution keeps
/// admin changes effective without a restart and picks up the bound addresses, which
/// only exist after the server has started. Read failures are logged and degrade to
/// the derived address — a transient database or DNS error must not take down the
/// discovery presence (fail-open).
/// </summary>
internal sealed class DiscoveryBaseUrlResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DiscoveryOptions> _options;
    private readonly IServer _server;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiscoveryBaseUrlResolver> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryBaseUrlResolver"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for reading the admin override from the database.</param>
    /// <param name="options">The bound <c>Discovery</c> options.</param>
    /// <param name="server">The running server (for the actually bound addresses).</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public DiscoveryBaseUrlResolver(
        IServiceScopeFactory scopeFactory,
        IOptions<DiscoveryOptions> options,
        IServer server,
        IConfiguration configuration,
        ILogger<DiscoveryBaseUrlResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _server = server;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the public base URL to report in the next discovery answer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public base URL to report.</returns>
    public async Task<string> ResolveAsync(CancellationToken cancellationToken)
    {
        string? adminPublicBaseUrl = null;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            adminPublicBaseUrl = await scope.ServiceProvider
                .GetRequiredService<ProgramSettingsService>()
                .GetDiscoveryPublicBaseUrlAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fail-open: Ein transienter DB-Fehler darf die Discovery-Präsenz nicht kippen,
            // bleibt aber diagnostizierbar.
            _logger.LogWarning(
                ex,
                "[DiscoveryBaseUrlResolver] Admin-Basis-URL konnte nicht gelesen werden; Ableitung ohne Admin-Override.");
        }

        var boundAddresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;

        // Hostname einmalig auflösen und sowohl für die Adressliste als auch für den
        // Hostname-Fallback der Ableitung wiederverwenden.
        string? hostName = null;
        IPAddress[]? hostAddresses = null;
        try
        {
            hostName = Dns.GetHostName();
            hostAddresses = (await Dns.GetHostEntryAsync(hostName, cancellationToken)).AddressList;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // DNS nicht auflösbar — Ableitung fällt auf den Hostnamen bzw. localhost zurück.
            _logger.LogWarning(
                ex,
                "[DiscoveryBaseUrlResolver] Host-Adressen konnten nicht per DNS aufgelöst werden; Ableitung fällt auf den Hostnamen zurück.");
        }

        return DiscoveryResponseBuilder.Build(
            _options.Value, _configuration, boundAddresses, hostAddresses, hostName, adminPublicBaseUrl);
    }
}
