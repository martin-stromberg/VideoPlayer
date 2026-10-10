using System.Net;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Pure derivation logic for the mDNS advertisement: resolves the announced port from
/// <c>Mdns:Port</c>, the bound server addresses, <c>Kestrel:Endpoints:Http:Url</c>,
/// <c>Host:Port</c> or the default 5000, and produces a testable <see cref="MdnsAdvertisement"/>
/// without touching the network.
/// </summary>
internal static class MdnsServiceProfileBuilder
{
    /// <summary>
    /// The fallback port used when no other source yields one.
    /// </summary>
    public const int DefaultPort = 5000;

    /// <summary>
    /// Builds the advertisement profile from options, configuration and the actually
    /// bound server addresses (<c>IServerAddressesFeature.Addresses</c>).
    /// </summary>
    /// <param name="options">The bound <c>Mdns</c> options.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="boundAddresses">The addresses the server actually bound, if already known.</param>
    /// <returns>The resolved advertisement.</returns>
    public static MdnsAdvertisement Build(
        MdnsOptions options,
        IConfiguration configuration,
        IEnumerable<string>? boundAddresses)
    {
        return new MdnsAdvertisement
        {
            InstanceName = options.InstanceName,
            ServiceType = options.ServiceType,
            Port = ResolvePort(options, configuration, boundAddresses),
            HostName = Dns.GetHostName(),
            TxtRecords = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = "/",
                ["app"] = "VideoWebPlayer"
            }
        };
    }

    private static int ResolvePort(MdnsOptions options, IConfiguration configuration, IEnumerable<string>? boundAddresses)
    {
        if (options.Port is >= 1 and <= 65535)
            return options.Port.Value;

        if (boundAddresses is not null)
        {
            foreach (var address in boundAddresses)
            {
                var port = TryGetPort(address);
                if (port is not null)
                    return port.Value;
            }
        }

        var kestrelPort = TryGetPort(configuration["Kestrel:Endpoints:Http:Url"]);
        if (kestrelPort is not null)
            return kestrelPort.Value;

        if (int.TryParse(configuration["Host:Port"], out var hostPort) && hostPort is >= 1 and <= 65535)
            return hostPort;

        return DefaultPort;
    }

    private static int? TryGetPort(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        // Kestrel-Wildcard-Hosts (* und +) sind keine gültigen URI-Hosts — für die
        // reine Port-Extraktion durch einen gültigen Host ersetzen.
        var normalized = address.Trim()
            .Replace("://*", "://localhost")
            .Replace("://+", "://localhost");

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        return uri.Port;
    }
}
