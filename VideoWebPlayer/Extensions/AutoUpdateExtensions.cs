using System.Net.Http.Headers;
using msTools.Updater;
using VideoWebPlayer.Services.Updates;

namespace VideoWebPlayer.Extensions;

/// <summary>
/// Registers the automatic program update subsystem (msTools.Updater) and the backup that is created before an
/// update is installed.
/// </summary>
public static class AutoUpdateExtensions
{
    /// <summary>
    /// The GitHub repository owner releases are read from.
    /// </summary>
    private const string ReleaseRepositoryOwner = "martin-stromberg";

    /// <summary>
    /// The GitHub repository releases are read from.
    /// </summary>
    private const string ReleaseRepositoryName = "VideoPlayer";

    /// <summary>
    /// The systemd unit name used on Linux to run the installation script.
    /// </summary>
    private const string UpdateUnitName = "VideoWebPlayer-AutoUpdate";

    /// <summary>
    /// The configuration key holding the GitHub token used to read releases of the (private) repository.
    /// </summary>
    private const string GitHubTokenConfigurationKey = "AutoUpdate:GitHubToken";

    /// <summary>
    /// Registers the auto-update subsystem. All values except the update source are taken from the
    /// <c>AutoUpdate</c> configuration section; the backup is configured in <c>AutoUpdate:Backup</c>.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddVideoWebPlayerAutoUpdate(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<UpdateBackupOptions>(builder.Configuration.GetSection(UpdateBackupOptions.SectionName));
        builder.Services.AddSingleton<UpdateBackupCoordinator>();

        builder.UseAutoUpdate(cfg =>
        {
            cfg.UseSource(CreateGithubSource(builder.Configuration))
               .WithUpdateUnitName(UpdateUnitName);
        });

        builder.Services.AddHostedService<UpdateBackupEventBinder>();

        return builder;
    }

    /// <summary>
    /// Creates the GitHub update source. Since the repository is private, a configured token is sent as a bearer
    /// token; without it the GitHub API answers with 404 and no update is found.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured update source.</returns>
    private static AutoUpdateGithubSource CreateGithubSource(IConfiguration configuration)
    {
        var allowPrereleaseUpdates = configuration.GetValue($"{AutoUpdateHostBuilderExtensions.DefaultConfigurationSectionName}:{nameof(AutoUpdateOptions.AllowPrereleaseUpdates)}", false);
        var token = configuration[GitHubTokenConfigurationKey];
        if (string.IsNullOrWhiteSpace(token))
        {
            return AutoUpdateGithubSource.Create(
                ReleaseRepositoryOwner,
                ReleaseRepositoryName,
                includePrereleases: allowPrereleaseUpdates);
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
            includePrereleases: allowPrereleaseUpdates);
    }
}
