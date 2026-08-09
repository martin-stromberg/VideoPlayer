using System.Net.Http.Headers;
using msTools.Updater;

namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Creates the GitHub update source used by VideoWebPlayer.
/// </summary>
public sealed class VideoWebPlayerUpdateSourceFactory
{
    private const string ReleaseRepositoryOwner = "martin-stromberg";
    private const string ReleaseRepositoryName = "VideoPlayer";
    private const string GitHubTokenConfigurationKey = "AutoUpdate:GitHubToken";

    private readonly IConfiguration _configuration;

    /// <summary>
    /// Creates a new update source factory.
    /// </summary>
    public VideoWebPlayerUpdateSourceFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Creates the configured GitHub update source.
    /// </summary>
    public AutoUpdateGithubSource Create(bool includePrereleases)
    {
        var token = _configuration[GitHubTokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            return AutoUpdateGithubSource.Create(
                ReleaseRepositoryOwner,
                ReleaseRepositoryName,
                includePrereleases: includePrereleases);
        }

        var httpClient = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VideoWebPlayer");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new AutoUpdateGithubSource(
            httpClient,
            ReleaseRepositoryOwner,
            ReleaseRepositoryName,
            includePrereleases: includePrereleases);
    }
}
