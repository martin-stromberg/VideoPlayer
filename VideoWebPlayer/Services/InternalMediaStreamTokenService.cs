using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Issues short-lived, in-memory stream tokens that authorize
/// <see cref="Controllers.InternalMediaStreamController"/> to stream exactly one media item,
/// and builds the loopback probe URI the media duration probe hands to ffprobe. Tokens are
/// cryptographically random (256 bit), bound to a single media item and expire after the
/// configured lifetime — they never persist, so a process restart simply turns an
/// outstanding probe into a transient failure that is retried later.
/// </summary>
public sealed class InternalMediaStreamTokenService : IInternalMediaStreamTokenService
{
    /// <summary>
    /// Route prefix of <see cref="Controllers.InternalMediaStreamController"/> — the token is
    /// appended as the final path segment.
    /// </summary>
    internal const string StreamRoutePrefix = "/api/internal/media-stream/";

    /// <summary>Host name handed to ffprobe for every wildcard/loopback binding.</summary>
    private const string LoopbackHost = "127.0.0.1";

    private const string HttpScheme = "http";

    private static readonly string[] WildcardHosts = { "*", "+", "", "0.0.0.0", "::" };

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    private readonly IServer? _server;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<InternalMediaStreamTokenService> _logger;
    private readonly TimeProvider _timeProvider;

    // The bound addresses never change after startup — resolve once, log once.
    private string? _resolvedBaseUrl;
    private bool _baseUrlResolved;

    private sealed record TokenEntry(long MediaItemId, DateTimeOffset ExpiresAt);

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalMediaStreamTokenService"/> class.
    /// </summary>
    /// <param name="server">The running web server, used to read the bound addresses.</param>
    /// <param name="options">Media probe options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    public InternalMediaStreamTokenService(
        IServer? server,
        IOptions<MediaProbeOptions> options,
        ILogger<InternalMediaStreamTokenService> logger,
        TimeProvider? timeProvider = null)
    {
        _server = server;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string? TryCreateProbeUri(long mediaItemId)
    {
        if (!_options.HttpProbeEnabled)
            return null;

        var baseUrl = ResolveBaseUrl();
        if (baseUrl is null)
            return null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = new TokenEntry(
            mediaItemId,
            _timeProvider.GetUtcNow().AddSeconds(_options.EffectiveHttpProbeTokenLifetimeSeconds));

        SweepExpiredTokens();

        return $"{baseUrl}{StreamRoutePrefix}{token}";
    }

    /// <inheritdoc />
    public long? ResolveMediaItemId(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        if (_tokens.TryGetValue(token, out var entry) && entry.ExpiresAt > _timeProvider.GetUtcNow())
            return entry.MediaItemId;

        _tokens.TryRemove(token, out _);
        return null;
    }

    /// <summary>
    /// Resolves the loopback base URL once per instance: the configured
    /// <see cref="MediaProbeOptions.HttpProbeBaseUrl"/> wins, otherwise the first bound
    /// <c>http://</c> address of the running server is normalized so ffprobe reaches this
    /// same process — a wildcard binding becomes <c>127.0.0.1</c>, a concrete address is
    /// kept (the machine can always reach its own bound address). Returns
    /// <see langword="null"/> when no <c>http</c> address exists; an HTTPS-only server is not
    /// usable for self-probes (ffprobe would fail TLS verification on dev/self-signed
    /// certificates), so the caller falls back to the other probe paths.
    /// </summary>
    private string? ResolveBaseUrl()
    {
        if (_baseUrlResolved)
            return _resolvedBaseUrl;

        _baseUrlResolved = true;

        var configured = _options.HttpProbeBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var normalized = TryNormalizeBaseUrl(configured.Trim());
            if (normalized is not null)
            {
                _resolvedBaseUrl = normalized;
                return _resolvedBaseUrl;
            }

            _logger.LogWarning(
                "MediaProbe:HttpProbeBaseUrl '{BaseUrl}' ist keine gültige http(s)-Basis-URL — die HTTP-Laufzeitermittlung versucht die automatische Erkennung.",
                configured);
        }

        var addresses = _server?.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is not null)
        {
            foreach (var address in addresses)
            {
                var normalized = TryNormalizeServerAddress(address);
                if (normalized is not null)
                {
                    _resolvedBaseUrl = normalized;
                    return _resolvedBaseUrl;
                }
            }
        }

        _logger.LogWarning(
            "Keine erreichbare http-Adresse der eigenen Instanz gefunden — die HTTP-Laufzeitermittlung ist deaktiviert und Remote-Dateien fallen auf sftp:///pipe:0-Probes zurück (MediaProbe:HttpProbeBaseUrl kann die Adresse explizit setzen).");

        return null;
    }

    /// <summary>
    /// Normalizes an explicitly configured base URL: must be an absolute
    /// <c>http(s)</c>-URI; a trailing slash is removed so the route prefix can be appended.
    /// </summary>
    private static string? TryNormalizeBaseUrl(string baseUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? baseUrl.TrimEnd('/')
                : null;

    /// <summary>
    /// Normalizes one entry of <see cref="IServerAddressesFeature.Addresses"/> to a base URL
    /// ffprobe can reach, or returns <see langword="null"/> for non-<c>http</c> or unparsable
    /// entries. <see cref="Uri"/> cannot parse wildcard hosts like <c>*</c>/<c>+</c>, so the
    /// address is split manually.
    /// </summary>
    /// <param name="address">A bound server address such as <c>http://*:5002</c>.</param>
    internal static string? TryNormalizeServerAddress(string address)
    {
        const string httpPrefix = "http://";
        if (string.IsNullOrWhiteSpace(address)
            || !address.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var hostPort = address[httpPrefix.Length..];
        var slash = hostPort.IndexOf('/');
        if (slash >= 0)
            hostPort = hostPort[..slash];

        string host;
        var port = 80;
        if (hostPort.StartsWith('['))
        {
            var closing = hostPort.IndexOf(']');
            if (closing < 0)
                return null;
            host = hostPort[1..closing];
            var rest = hostPort[(closing + 1)..];
            if (!TryParsePortSuffix(rest, out port))
                return null;
        }
        else
        {
            var colon = hostPort.LastIndexOf(':');
            if (colon >= 0)
            {
                host = hostPort[..colon];
                if (!int.TryParse(hostPort[(colon + 1)..], out port))
                    return null;
            }
            else
            {
                host = hostPort;
            }
        }

        var normalizedHost = IsWildcardOrLoopback(host) ? LoopbackHost : host;
        return $"{httpPrefix}{normalizedHost}:{port}";
    }

    private static bool TryParsePortSuffix(string suffix, out int port)
    {
        if (suffix.Length == 0)
        {
            port = 80;
            return true;
        }

        port = 0;
        return suffix.StartsWith(':') && int.TryParse(suffix[1..], out port);
    }

    private static bool IsWildcardOrLoopback(string host)
    {
        foreach (var wildcard in WildcardHosts)
        {
            if (string.Equals(host, wildcard, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes expired tokens so the store stays bounded. Called on each issue — the cost is
    /// proportional to the number of outstanding (batch-sized) tokens.
    /// </summary>
    private void SweepExpiredTokens()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _tokens)
        {
            if (pair.Value.ExpiresAt <= now)
                _tokens.TryRemove(pair.Key, out _);
        }
    }
}
