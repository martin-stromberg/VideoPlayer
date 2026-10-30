using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
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
public sealed partial class InternalMediaStreamTokenService : IInternalMediaStreamTokenService
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

    /// <summary>
    /// Masks a media-stream token wherever it appears in text handed to logs or external
    /// process output — the token sits in the URI path (final segment after
    /// <see cref="StreamRoutePrefix"/>), so credential-only masking does not catch it.
    /// </summary>
    /// <param name="text">Text that may contain a probe URI with a token.</param>
    /// <returns>The text with any stream token replaced by <c>***</c>.</returns>
    internal static string MaskStreamTokenInText(string? text)
        => string.IsNullOrEmpty(text)
            ? string.Empty
            : StreamTokenRegex().Replace(text, $"{StreamRoutePrefix}***");

    /// <summary>Matches a stream route prefix followed by a token-like hex tail.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"/api/internal/media-stream/[0-9a-fA-F]{16,}")]
    private static partial System.Text.RegularExpressions.Regex StreamTokenRegex();

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    private readonly IServer? _server;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<InternalMediaStreamTokenService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    // The bound addresses never change after startup — resolve once, log once. A negative
    // result is NOT cached: at startup Addresses can still be empty, and retrying on the
    // next probe is cheap, while permanently disabling HTTP probing would be wrong.
    private string? _resolvedBaseUrl;
    private bool _baseUrlResolved;
    private bool _noAddressWarned;
    private bool _invalidConfigWarned;

    private sealed record TokenEntry(long MediaItemId, DateTimeOffset ExpiresAt);

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalMediaStreamTokenService"/> class.
    /// </summary>
    /// <param name="server">The running web server, used to read the bound addresses.</param>
    /// <param name="options">Media probe options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    /// <param name="configuration">Optional application configuration, used to read the
    /// configured <c>Kestrel:Endpoints</c> as the last base-URL detection stage.</param>
    public InternalMediaStreamTokenService(
        IServer? server,
        IOptions<MediaProbeOptions> options,
        ILogger<InternalMediaStreamTokenService> logger,
        TimeProvider? timeProvider = null,
        IConfiguration? configuration = null)
    {
        _server = server;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
    }

    /// <summary>The shortest lifetime a stream token is ever issued with.</summary>
    private static readonly TimeSpan MinimumStreamTokenLifetime = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public string? TryCreateProbeUri(long mediaItemId)
    {
        if (!_options.HttpProbeEnabled)
            return null;

        return TryCreateUri(mediaItemId, TimeSpan.FromSeconds(_options.EffectiveHttpProbeTokenLifetimeSeconds));
    }

    /// <inheritdoc />
    public string? TryCreateStreamUri(long mediaItemId, TimeSpan lifetime)
        => TryCreateUri(
            mediaItemId,
            lifetime > MinimumStreamTokenLifetime ? lifetime : MinimumStreamTokenLifetime);

    /// <summary>
    /// Issues a stream token with the given lifetime and returns the absolute loopback URI.
    /// </summary>
    /// <param name="mediaItemId">The media item the token authorizes.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    /// <returns>The stream URI, or <see langword="null"/> when no HTTP address is available.</returns>
    private string? TryCreateUri(long mediaItemId, TimeSpan lifetime)
    {
        var baseUrl = ResolveBaseUrl();
        if (baseUrl is null)
            return null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = new TokenEntry(
            mediaItemId,
            _timeProvider.GetUtcNow().Add(lifetime));

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
    /// kept (the machine can always reach its own bound address). As a final stage the
    /// configured <c>Kestrel:Endpoints</c> are enumerated, because endpoints declared only
    /// in configuration never appear in <see cref="IServerAddressesFeature.Addresses"/>.
    /// Returns <see langword="null"/> when no <c>http</c> address exists; an HTTPS-only
    /// server is not usable for self-probes (ffprobe would fail TLS verification on
    /// dev/self-signed certificates), so the caller falls back to the other probe paths.
    /// </summary>
    private string? ResolveBaseUrl()
    {
        if (_baseUrlResolved)
            return _resolvedBaseUrl;

        var configured = _options.HttpProbeBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var normalized = TryNormalizeBaseUrl(configured.Trim());
            if (normalized is not null)
            {
                _resolvedBaseUrl = normalized;
                _baseUrlResolved = true;
                return _resolvedBaseUrl;
            }

            if (!_invalidConfigWarned)
            {
                _invalidConfigWarned = true;
                _logger.LogWarning(
                    "MediaProbe:HttpProbeBaseUrl '{BaseUrl}' ist keine gültige http(s)-Basis-URL — die HTTP-Laufzeitermittlung versucht die automatische Erkennung.",
                    configured);
            }
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
                    _baseUrlResolved = true;
                    return _resolvedBaseUrl;
                }
            }
        }

        // Last resort: the configured Kestrel endpoints. Endpoint names are free-form, so
        // every child is inspected; an endpoint restricted to protocols without
        // Http1/Http2 (e.g. pure Http3 over UDP) cannot serve the loopback TCP probe.
        foreach (var endpoint in _configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            var protocols = endpoint["Protocols"];
            if (!string.IsNullOrEmpty(protocols)
                && !protocols.Contains("Http1", StringComparison.OrdinalIgnoreCase)
                && !protocols.Contains("Http2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = endpoint["Url"];
            if (string.IsNullOrEmpty(url))
                continue;

            var normalized = TryNormalizeServerAddress(url);
            if (normalized is not null)
            {
                _resolvedBaseUrl = normalized;
                _baseUrlResolved = true;
                return _resolvedBaseUrl;
            }
        }

        // Deliberately not marking _baseUrlResolved: the addresses may simply not be
        // populated yet (startup race), and the next probe should try again — a one-off
        // empty read must not disable HTTP probing for the whole process lifetime.
        if (!_noAddressWarned)
        {
            _noAddressWarned = true;
            _logger.LogWarning(
                "Keine erreichbare http-Adresse der eigenen Instanz gefunden — die HTTP-Laufzeitermittlung und der Live-Stream-HTTP-Eingang pausieren und Remote-Dateien fallen vorerst auf sftp:///pipe:0 zurück (geprüft: MediaProbe:HttpProbeBaseUrl, gebundene Server-Adressen und Kestrel:Endpoints; MediaProbe:HttpProbeBaseUrl kann die Adresse explizit setzen).");
        }

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

        // A dynamic port binding (":0") can never serve as the loopback base URL.
        if (port <= 0)
            return null;

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
