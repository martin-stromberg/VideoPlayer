using System.Net;
using System.Net.Sockets;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Pure derivation logic for the UDP discovery answer: resolves the public base URL the
/// server reports in <c>VIDEOWEBPLAYER_SERVER</c> responses — the admin-maintained value
/// (<c>Setup.DiscoveryPublicBaseUrl</c>) wins over the operator configuration
/// (<c>Discovery:PublicBaseUrl</c>), and only when both are empty the URL is derived from
/// the bound server addresses, the Kestrel configuration, <c>Host:Address</c>/<c>Host:Port</c>
/// and the host's LAN addresses. Loopback and wildcard addresses are never reported.
/// </summary>
internal static class DiscoveryResponseBuilder
{
    /// <summary>
    /// The fallback port used when no other source yields one.
    /// </summary>
    public const int DefaultPort = 5000;

    /// <summary>
    /// Builds the public base URL reported in discovery answers.
    /// </summary>
    /// <param name="options">The bound <c>Discovery</c> options.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="boundAddresses">The addresses the server actually bound (<c>IServerAddressesFeature.Addresses</c>), if already known.</param>
    /// <param name="hostAddresses">The DNS address list of the host (<c>Dns.GetHostEntry</c>), if resolvable.</param>
    /// <param name="hostName">The machine host name, resolved once by the caller (<c>Dns.GetHostName</c>); used as the last-resort host.</param>
    /// <param name="adminPublicBaseUrl">The current admin value from <c>Setups</c>, if any.</param>
    /// <returns>The public base URL to report.</returns>
    public static string Build(
        DiscoveryOptions options,
        IConfiguration configuration,
        IEnumerable<string>? boundAddresses,
        IReadOnlyList<IPAddress>? hostAddresses,
        string? hostName,
        string? adminPublicBaseUrl)
    {
        var adminUrl = DiscoveryUrlRules.NormalizePublicBaseUrl(adminPublicBaseUrl);
        if (adminUrl is not null && DiscoveryUrlRules.IsValidPublicBaseUrl(adminUrl))
            return adminUrl;

        var configuredUrl = DiscoveryUrlRules.NormalizePublicBaseUrl(options.PublicBaseUrl);
        if (configuredUrl is not null)
            return configuredUrl;

        var (scheme, port) = ResolveSchemeAndPort(configuration, boundAddresses);
        var host = ResolveHost(configuration, boundAddresses, hostAddresses, hostName);
        return $"{scheme}://{host}:{port}";
    }

    private static (string Scheme, int Port) ResolveSchemeAndPort(
        IConfiguration configuration,
        IEnumerable<string>? boundAddresses)
    {
        if (boundAddresses is not null)
        {
            foreach (var address in boundAddresses)
            {
                var schemeAndPort = TryGetSchemeAndPort(address);
                if (schemeAndPort is not null)
                    return schemeAndPort.Value;
            }
        }

        var http = TryGetSchemeAndPort(configuration["Kestrel:Endpoints:Http:Url"]);
        if (http is not null)
            return http.Value;

        var https = TryGetSchemeAndPort(configuration["Kestrel:Endpoints:Https:Url"]);
        if (https is not null)
            return https.Value;

        if (int.TryParse(configuration["Host:Port"], out var hostPort) && hostPort is >= 1 and <= 65535)
            return (Uri.UriSchemeHttp, hostPort);

        return (Uri.UriSchemeHttp, DefaultPort);
    }

    private static (string Scheme, int Port)? TryGetSchemeAndPort(string? address)
    {
        var uri = TryCreateNormalizedUri(address);
        if (uri is null)
            return null;

        return (uri.Scheme, uri.Port);
    }

    private static string ResolveHost(
        IConfiguration configuration,
        IEnumerable<string>? boundAddresses,
        IReadOnlyList<IPAddress>? hostAddresses,
        string? hostName)
    {
        var configuredHost = configuration["Host:Address"];
        if (IsUsableHost(configuredHost))
            return configuredHost!.Trim();

        foreach (var candidate in EnumerateAddressSources(configuration, boundAddresses))
        {
            var uri = TryCreateNormalizedUri(candidate);
            if (uri is not null && IsUsableHost(uri.DnsSafeHost))
                return uri.Host;
        }

        if (hostAddresses is not null)
        {
            IPAddress? apipaAddress = null;
            foreach (var address in hostAddresses)
            {
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    continue;

                // APIPA (169.254.x.x) is reachable on the link but only a last-resort LAN
                // identity — remember it and keep looking for a real address.
                if (IsApipaAddress(address))
                {
                    apipaAddress ??= address;
                    continue;
                }

                return address.ToString();
            }

            if (apipaAddress is not null)
                return apipaAddress.ToString();
        }

        return string.IsNullOrWhiteSpace(hostName) ? "localhost" : hostName.Trim();
    }

    private static IEnumerable<string?> EnumerateAddressSources(
        IConfiguration configuration,
        IEnumerable<string>? boundAddresses)
    {
        if (boundAddresses is not null)
        {
            foreach (var address in boundAddresses)
                yield return address;
        }

        yield return configuration["Kestrel:Endpoints:Http:Url"];
        yield return configuration["Kestrel:Endpoints:Https:Url"];
    }

    private static Uri? TryCreateNormalizedUri(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        // Kestrel-Wildcard-Hosts (* und +) sind keine gültigen URI-Hosts — für die
        // reine Port-/Host-Extraktion durch einen gültigen Host ersetzen.
        var normalized = address.Trim()
            .Replace("://*", "://localhost")
            .Replace("://+", "://localhost");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri;
    }

    private static bool IsUsableHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var trimmed = host.Trim();
        if (trimmed is "*" or "+" or "0.0.0.0" or "[::]")
            return false;
        if (string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IPAddress.TryParse(trimmed, out var address))
        {
            if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
                return false;

            // IPv6-Adressen werden für den Host-Teil nicht gemeldet (Klammer-Notation und
            // Link-Local-Handling sind für LAN-Clients fehleranfällig).
            if (address.AddressFamily == AddressFamily.InterNetworkV6)
                return false;
        }

        return true;
    }

    private static bool IsApipaAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }
}
