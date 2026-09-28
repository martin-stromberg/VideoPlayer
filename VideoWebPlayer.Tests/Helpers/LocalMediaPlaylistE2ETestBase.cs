using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared setup for the E2E tests that combine a media source of type "local directory" with playlists
/// (A7): a hosted application on a temp SQLite database (<see cref="PairingWebApplicationFactory"/>), a real
/// directory on disk with real files, a media source of type
/// <see cref="MediaSourceType.LocalDirectory"/> pointing at it, and a logged-in
/// <see cref="VideoWebPlayerClient"/> for the playlist API. Scanning and classifying run through the
/// application's own <see cref="MediaSourceScanner"/>/<see cref="MediaSourceClassifier"/>, exactly as the
/// admin pages drive them - nothing about the media is faked.
/// </summary>
public abstract class LocalMediaPlaylistE2ETestBase : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    /// <summary>The real directory on disk the media source reads from.</summary>
    protected string RootDirectory { get; }

    /// <summary>The playlist/media API client, logged in as <see cref="User"/>.</summary>
    protected VideoWebPlayerClient Client { get; private set; } = null!;

    /// <summary>The test user; an administrator, so they may also delete media sources.</summary>
    protected ApplicationUser User { get; private set; } = null!;

    /// <summary>The bearer token of <see cref="User"/>, for the raw HTTP calls of some tests.</summary>
    protected string SessionToken { get; private set; } = string.Empty;

    /// <summary>The id of the media source reading <see cref="RootDirectory"/>.</summary>
    protected long MediaSourceId { get; private set; }

    /// <summary>The application's service provider, for seeding and for checking server state.</summary>
    protected IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Creates the test host and the empty directory the media source will read; the concrete test fills
    /// the directory before calling <see cref="ScanEverythingAsync"/>.
    /// </summary>
    protected LocalMediaPlaylistE2ETestBase()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-local-playlist");
        RootDirectory = Path.Combine(Path.GetTempPath(), $"vwp-local-playlist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootDirectory);

        _factory = PairingWebApplicationFactory.Create(_dbPath, builder =>
            builder.UseSetting("AutoUpdate:HostedServicesEnabled", "false"));
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        Client = new VideoWebPlayerClient(_factory.CreateDefaultClient(), NullLogger<VideoWebPlayerClient>.Instance);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>();

        var email = $"lokale-quelle-{Guid.NewGuid():N}@test.com";
        User = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsAdmin = true };
        var created = await userManager.CreateAsync(User);
        Assert.True(created.Succeeded, string.Join(Environment.NewLine, created.Errors.Select(e => e.Description)));

        MediaSourceId = await AddLocalMediaSourceAsync(db, "Lokale Playlist-Quelle", RootDirectory, ct);

        var token = tokenService.CreateToken(User);
        SessionToken = token.token;
        Client.SetAuthorizationToken(token);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
        try { Directory.Delete(RootDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { /* best effort */ }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a second media source of type "local directory" on a fresh directory, for the tests that
    /// need a title which survives the deletion of the first source.
    /// </summary>
    /// <param name="name">The display name of the media source.</param>
    /// <returns>The directory on disk and the id of the created media source.</returns>
    /// <!-- Tupel-Elemente: Directory, MediaSourceId -->
    protected async Task<(string Directory, long MediaSourceId)> CreateSecondLocalSourceAsync(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), $"vwp-local-playlist-2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (directory, await AddLocalMediaSourceAsync(db, name, directory, ct));
    }

    /// <summary>
    /// Writes a movie the classifier recognizes: the video file, its NFO and - on request - a real poster
    /// image next to it (<c>&lt;name&gt;-poster.png</c>, the naming <c>MediaSourceClassifier</c> looks for).
    /// </summary>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="fileBaseName">The base file name, without extension.</param>
    /// <param name="title">The movie title written into the NFO.</param>
    /// <param name="withPoster">Whether to write a poster image next to the video.</param>
    /// <param name="releaseYear">The release year written into the NFO, which decides the order inside a playlist sorted by release date.</param>
    /// <returns>The bytes written as the video file, so a streaming test can compare against them.</returns>
    protected static byte[] WriteMovieFiles(string directory, string fileBaseName, string title, bool withPoster = false, int? releaseYear = null)
    {
        Directory.CreateDirectory(directory);
        var videoBytes = System.Text.Encoding.UTF8.GetBytes($"video-{fileBaseName}");
        File.WriteAllBytes(Path.Combine(directory, $"{fileBaseName}.mp4"), videoBytes);

        var releaseDate = releaseYear is int year ? $"<releasedate>{year}-01-01</releasedate>" : string.Empty;
        File.WriteAllText(Path.Combine(directory, $"{fileBaseName}.nfo"), $"<movie><title>{title}</title>{releaseDate}</movie>");
        if (withPoster)
            File.WriteAllBytes(Path.Combine(directory, $"{fileBaseName}-poster.png"), TestImages.Png(200, 300));

        return videoBytes;
    }

    /// <summary>
    /// Writes one episode of a TV show the classifier recognizes; the show's own NFO is written on the first
    /// call for that directory.
    /// </summary>
    /// <param name="directory">The show's directory.</param>
    /// <param name="showTitle">The show title written into <c>tvshow.nfo</c>.</param>
    /// <param name="fileBaseName">The base file name of the episode, without extension.</param>
    /// <param name="episodeTitle">The episode title written into its NFO.</param>
    /// <param name="episodeNumber">The episode number within season 1.</param>
    protected static void WriteEpisodeFiles(string directory, string showTitle, string fileBaseName, string episodeTitle, int episodeNumber)
    {
        Directory.CreateDirectory(directory);
        var showNfo = Path.Combine(directory, "tvshow.nfo");
        if (!File.Exists(showNfo))
            File.WriteAllText(showNfo, $"<tvshow><title>{showTitle}</title></tvshow>");

        File.WriteAllText(Path.Combine(directory, $"{fileBaseName}.mp4"), $"video-{fileBaseName}");
        File.WriteAllText(
            Path.Combine(directory, $"{fileBaseName}.nfo"),
            $"<episodedetails><title>{episodeTitle}</title><season>1</season><episode>{episodeNumber}</episode></episodedetails>");
    }

    /// <summary>
    /// Runs the application's own full scan and classification over every media source, the way the initial
    /// import does.
    /// </summary>
    protected async Task ScanEverythingAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
        var signal = scope.ServiceProvider.GetRequiredService<IPlaylistBackfillSignal>();

        using (signal.BeginScan())
        {
            await scanner.ScanAllSourcesAsync(ct);
            while (await scanner.ScanNextMediaCollection(ct))
            {
            }

            await classifier.ClassifyAllAsync(ct);
        }
    }

    /// <summary>
    /// Re-reads one directory exactly as the "Neu erfassen" button of the media-source explorer does
    /// (<c>MediaSourceExplorer.RescanCurrent</c>): scan of the collection tree and classification of the same
    /// tree, bracketed by a backfill scan scope.
    /// </summary>
    /// <param name="collectionId">The id of the media collection (directory) to re-read.</param>
    protected async Task RescanCollectionAsync(long collectionId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
        var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();
        var signal = scope.ServiceProvider.GetRequiredService<IPlaylistBackfillSignal>();

        using (signal.BeginScan())
        {
            await scanner.ScanCollectionTreeAsync(collectionId, ct);
            await classifier.ClassifyCollectionTreeAsync(collectionId, ct);
        }
    }

    /// <summary>
    /// Runs the pending automatic playlist backfill, the work <c>PlaylistBackfillWorker</c> hands to
    /// <see cref="PlaylistBackfillCoordinator"/> after a scan - here triggered directly so the test does not
    /// depend on the worker's settle time.
    /// </summary>
    /// <returns>What the run did.</returns>
    protected async Task<PlaylistBackfillRunSummary> RunPlaylistBackfillAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        return await _factory.Services.GetRequiredService<PlaylistBackfillCoordinator>().ProcessPendingAsync(ct);
    }

    /// <summary>
    /// Creates a plain HTTP client against the hosted application, for the calls that are about HTTP
    /// semantics themselves (streaming, cover images, admin endpoints).
    /// </summary>
    /// <returns>The new HTTP client.</returns>
    protected HttpClient CreateHttpClient() => _factory.CreateClient();

    /// <summary>
    /// Opens a scope on the hosted application's services and hands its database context to
    /// <paramref name="action"/>, for the assertions that read server state directly.
    /// </summary>
    /// <typeparam name="T">The result type produced by <paramref name="action"/>.</typeparam>
    /// <param name="action">The callback to run against the scoped database context.</param>
    /// <returns>The result produced by <paramref name="action"/>.</returns>
    protected async Task<T> ReadDatabaseAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>
    /// Resolves the id of a classified movie by the title its NFO carries.
    /// </summary>
    /// <param name="name">The movie title.</param>
    /// <returns>The movie's id.</returns>
    protected Task<long> GetMovieIdAsync(string name)
        => ReadDatabaseAsync(db => db.Movies.AsNoTracking().Where(m => m.Name == name).Select(m => m.Id)
            .SingleAsync(TestContext.Current.CancellationToken));

    /// <summary>
    /// Creates a playlist holding the given movies in the given order (sorted by release date).
    /// </summary>
    /// <param name="name">The name of the playlist to create.</param>
    /// <param name="movieIds">The ids of the movies to add.</param>
    /// <returns>The id of the created playlist.</returns>
    protected async Task<long> CreatePlaylistAsync(string name, params long[] movieIds)
    {
        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest
        {
            Name = name,
            SortMode = PlaylistSortModeValues.ByReleaseDate
        });
        foreach (var movieId in movieIds)
        {
            await Client.AddMediaToPlaylistAsync(playlist.Id,
                new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        }

        return playlist.Id;
    }

    private async Task<long> AddLocalMediaSourceAsync(ApplicationDbContext db, string name, string path, CancellationToken ct)
    {
        var source = new MediaSource
        {
            Name = name,
            Path = path,
            SourceType = MediaSourceType.LocalDirectory,
            Host = string.Empty,
            Port = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = User.Id });
        await db.SaveChangesAsync(ct);
        return source.Id;
    }
}
