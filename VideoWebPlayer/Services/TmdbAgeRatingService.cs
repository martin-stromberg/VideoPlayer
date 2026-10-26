using System.Text.Json;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Fehlertoleranter TMDB-Lookup für Altersfreigaben: Filme über
/// <c>release_dates</c>, Serien über <c>content_ratings</c>; direkt per TMDB-ID
/// oder über die Titelsuche. Liefert <c>null</c> statt Ausnahmen, wenn der
/// Dienst deaktiviert, nicht konfiguriert oder nicht erreichbar ist.
/// </summary>
public sealed class TmdbAgeRatingService : ITmdbAgeRatingService
{
    /// <summary>
    /// Name des über <see cref="IHttpClientFactory"/> konfigurierten HTTP-Clients.
    /// </summary>
    internal const string HttpClientName = "Tmdb";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TmdbOptions _options;
    private readonly ILogger<TmdbAgeRatingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbAgeRatingService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory for the named "Tmdb" client.</param>
    /// <param name="options">TMDB options.</param>
    /// <param name="logger">Logger instance.</param>
    public TmdbAgeRatingService(
        IHttpClientFactory httpClientFactory,
        IOptions<TmdbOptions> options,
        ILogger<TmdbAgeRatingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    private bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <inheritdoc />
    public Task<TmdbAgeRating?> GetMovieAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken)
        => GetAgeRatingCoreAsync(isMovie: true, title, year, tmdbId, cancellationToken);

    /// <inheritdoc />
    public Task<TmdbAgeRating?> GetTVShowAgeRatingAsync(string title, int? year, string? tmdbId, CancellationToken cancellationToken)
        => GetAgeRatingCoreAsync(isMovie: false, title, year, tmdbId, cancellationToken);

    private async Task<TmdbAgeRating?> GetAgeRatingCoreAsync(bool isMovie, string title, int? year, string? tmdbId, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            return null;

        try
        {
            var id = string.IsNullOrWhiteSpace(tmdbId) ? null : tmdbId.Trim();
            id ??= await SearchIdAsync(isMovie, title, year, cancellationToken);
            if (id is null)
                return null;

            return isMovie
                ? await GetMovieRatingAsync(id, cancellationToken)
                : await GetTVShowRatingAsync(id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TMDB-Altersfreigabe für '{Title}' fehlgeschlagen.", title);
            return null;
        }
    }

    private async Task<string?> SearchIdAsync(bool isMovie, string title, int? year, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var url = isMovie
            ? BuildUrl($"/search/movie", $"&query={Uri.EscapeDataString(title.Trim())}")
            : BuildUrl($"/search/tv", $"&query={Uri.EscapeDataString(title.Trim())}");
        if (year.HasValue)
            url += isMovie ? $"&year={year.Value}" : $"&first_air_date_year={year.Value}";

        var root = await GetJsonAsync(url, cancellationToken);
        if (root is null || !root.Value.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var entry in results.EnumerateArray())
        {
            if (!entry.TryGetProperty("id", out var idProperty))
                continue;
            var id = idProperty.ValueKind == JsonValueKind.Number
                ? idProperty.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : idProperty.GetString();
            if (!string.IsNullOrWhiteSpace(id))
                return id;
        }

        return null;
    }

    private async Task<TmdbAgeRating?> GetMovieRatingAsync(string id, CancellationToken cancellationToken)
    {
        var root = await GetJsonAsync(BuildUrl($"/movie/{Uri.EscapeDataString(id)}/release_dates"), cancellationToken);
        if (root is null || !root.Value.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        return SelectCountryRating(results, entry =>
        {
            if (entry.TryGetProperty("release_dates", out var dates) && dates.ValueKind == JsonValueKind.Array)
            {
                foreach (var date in dates.EnumerateArray())
                {
                    if (date.TryGetProperty("certification", out var certification)
                        && certification.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(certification.GetString()))
                    {
                        return certification.GetString();
                    }
                }
            }
            return null;
        });
    }

    private async Task<TmdbAgeRating?> GetTVShowRatingAsync(string id, CancellationToken cancellationToken)
    {
        var root = await GetJsonAsync(BuildUrl($"/tv/{Uri.EscapeDataString(id)}/content_ratings"), cancellationToken);
        if (root is null || !root.Value.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        return SelectCountryRating(results, entry =>
            entry.TryGetProperty("rating", out var rating)
            && rating.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(rating.GetString())
                ? rating.GetString()
                : null);
    }

    private TmdbAgeRating? SelectCountryRating(JsonElement results, Func<JsonElement, string?> certificationSelector)
    {
        var preferred = _options.EffectivePreferredCountry;
        TmdbAgeRating? unitedStates = null;
        TmdbAgeRating? first = null;

        foreach (var entry in results.EnumerateArray())
        {
            if (!entry.TryGetProperty("iso_3166_1", out var iso) || iso.ValueKind != JsonValueKind.String)
                continue;
            var country = iso.GetString();
            if (string.IsNullOrWhiteSpace(country))
                continue;

            var certification = certificationSelector(entry);
            if (string.IsNullOrWhiteSpace(certification))
                continue;

            if (string.Equals(country, preferred, StringComparison.OrdinalIgnoreCase))
                return new TmdbAgeRating(country, certification.Trim());
            unitedStates ??= string.Equals(country, "US", StringComparison.OrdinalIgnoreCase)
                ? new TmdbAgeRating(country, certification.Trim())
                : null;
            first ??= new TmdbAgeRating(country, certification.Trim());
        }

        return unitedStates ?? first;
    }

    private string BuildUrl(string path, string extraQuery = "")
        => $"{_options.EffectiveBaseUrl}{path}?api_key={Uri.EscapeDataString(_options.ApiKey!)}{extraQuery}";

    private async Task<JsonElement?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds));

        using var response = await client.GetAsync(url, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("TMDB-Anfrage an {Path} lieferte HTTP {StatusCode}.",
                new Uri(url).AbsolutePath, (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
