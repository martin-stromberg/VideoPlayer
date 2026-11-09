namespace VideoWebPlayer.Configuration;

/// <summary>
/// Options for the optional TMDB-based age rating lookup (section <c>Tmdb</c>).
/// The service stays inert unless <see cref="Enabled"/> is set and an
/// <see cref="ApiKey"/> is configured outside the repository
/// (<c>appsettings.Local.json</c>, environment variable or user secrets).
/// </summary>
public sealed class TmdbOptions
{
    private const string DefaultBaseUrl = "https://api.themoviedb.org/3";
    private const string DefaultPreferredCountry = "DE";
    private const int DefaultTimeoutSeconds = 10;

    /// <summary>
    /// Gets or sets whether the online age rating lookup is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the TMDB API key — never committed to the repository.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the TMDB API base URL (overridable for tests or mirrors).
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// Gets or sets the preferred country whose certification wins when TMDB
    /// reports several (fallback "US", then the first entry).
    /// </summary>
    public string PreferredCountry { get; set; } = DefaultPreferredCountry;

    /// <summary>
    /// Gets or sets the timeout of a single TMDB request in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;

    /// <summary>
    /// Gets the normalized base URL without a trailing slash.
    /// </summary>
    public string EffectiveBaseUrl
        => string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.Trim().TrimEnd('/');

    /// <summary>
    /// Gets the normalized preferred country tag (upper case ISO code).
    /// </summary>
    public string EffectivePreferredCountry
        => string.IsNullOrWhiteSpace(PreferredCountry) ? DefaultPreferredCountry : PreferredCountry.Trim().ToUpperInvariant();

    /// <summary>
    /// Gets the normalized request timeout (values &lt;= 0 fall back to the default).
    /// </summary>
    public int EffectiveTimeoutSeconds => TimeoutSeconds > 0 ? TimeoutSeconds : DefaultTimeoutSeconds;
}
