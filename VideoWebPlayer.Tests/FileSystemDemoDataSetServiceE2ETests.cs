using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.DemoData;
using VideoWebPlayer.Tests.Helpers;
using VideoWebPlayer.Tests.Infrastructure.Testing;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Tests für den mitgelieferten Demodatenbestand „Demobibliothek":
/// ApplyAsync legt eine LocalDirectory-Quelle an, materialisiert das
/// Medienverzeichnis auf der Platte und füllt die Bibliothek über den
/// angestoßenen Scan — gegen echten Host, echte SQLite-DB und echtes Dateisystem.
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class FileSystemDemoDataSetServiceE2ETests : IDisposable
{
    private const string DemoDataSetId = "Demobibliothek";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public FileSystemDemoDataSetServiceE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-demodata-{Guid.NewGuid()}.db");
        TempDirectoryCleanup.DeleteFileBestEffort(_dbPath);

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:Issuer", "VideoWebPlayer.Tests");
                builder.UseSetting("Jwt:ApiToken", "test-api-token");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
            });
    }

    [Fact]
    public async Task GetAvailableAsync_ReturnsDemobibliothek()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDemoDataSetService>();

        var sets = await service.GetAvailableAsync(TestContext.Current.CancellationToken);

        var set = Assert.Single(sets, s => s.Id == DemoDataSetId);
        Assert.False(string.IsNullOrWhiteSpace(set.Name));
        Assert.False(string.IsNullOrWhiteSpace(set.Description));
    }

    [Fact]
    public async Task DemoVideoFile_IsValidMp4Container()
    {
        var env = _factory.Services.GetRequiredService<IWebHostEnvironment>();
        var videoPath = Path.Combine(env.ContentRootPath, "DemoDataSets", DemoDataSetId, "demo-video.mp4");

        Assert.True(File.Exists(videoPath), $"Dummy-Videodatei fehlt: {videoPath}");
        var bytes = await File.ReadAllBytesAsync(videoPath, TestContext.Current.CancellationToken);
        var content = System.Text.Encoding.ASCII.GetString(bytes);

        Assert.Contains("ftyp", content[..Math.Min(64, content.Length)]);
        Assert.Contains("moov", content);
        Assert.Contains("trak", content);
        Assert.Contains("avc1", content);
    }

    [Fact]
    public async Task ApplyAsync_CreatesLocalDirectorySourceWithAccess()
    {
        var user = await CreateUserAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDemoDataSetService>();
            await service.ApplyAsync(DemoDataSetId, user, TestContext.Current.CancellationToken);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var source = await db.MediaSources.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(MediaSourceType.LocalDirectory, source.SourceType);
            Assert.True(Path.IsPathRooted(source.Path));
            Assert.True(Directory.Exists(source.Path));

            var grant = await db.MediaSourceUsers
                .SingleAsync(mu => mu.MediaSourceId == source.Id, TestContext.Current.CancellationToken);
            Assert.Equal(user.Id, grant.UserId);
        }
    }

    [Fact]
    public async Task ApplyAsync_MaterializesMediaDirectory()
    {
        var user = await CreateUserAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDemoDataSetService>();
            await service.ApplyAsync(DemoDataSetId, user, TestContext.Current.CancellationToken);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var source = await db.MediaSources.SingleAsync(TestContext.Current.CancellationToken);

            var mp4s = Directory.GetFiles(source.Path, "*.mp4", SearchOption.AllDirectories);
            var nfos = Directory.GetFiles(source.Path, "*.nfo", SearchOption.AllDirectories);
            var jpgs = Directory.GetFiles(source.Path, "*.jpg", SearchOption.AllDirectories);

            Assert.Equal(9, mp4s.Length);
            Assert.True(nfos.Length >= 11, $"Mindestens 11 NFO-Dateien erwartet, gefunden: {nfos.Length}");
            Assert.True(jpgs.Length >= 15, $"Mindestens 15 Bilddateien erwartet, gefunden: {jpgs.Length}");
            Assert.Contains(nfos, f => Path.GetFileName(f) == "tvshow.nfo");
            Assert.All(mp4s, f => Assert.True(new FileInfo(f).Length > 0));
        }
    }

    [Fact]
    public async Task ApplyAsync_LibraryIsFilledAfterScan()
    {
        var user = await CreateUserAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDemoDataSetService>();
            await service.ApplyAsync(DemoDataSetId, user, TestContext.Current.CancellationToken);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var movies = await db.Movies.ToListAsync(TestContext.Current.CancellationToken);
            var shows = await db.TVShows.ToListAsync(TestContext.Current.CancellationToken);
            var seasons = await db.TVShowSeasons.ToListAsync(TestContext.Current.CancellationToken);
            var episodes = await db.TVShowEpisodes.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(3, movies.Count);
            Assert.Equal(2, shows.Count);
            Assert.Equal(2, seasons.Count);
            Assert.Equal(6, episodes.Count);

            Assert.All(movies, m => Assert.NotNull(m.PosterPictureId));
            Assert.All(shows, s => Assert.NotNull(s.PosterPictureId));
            Assert.All(episodes, e => Assert.NotNull(e.PosterPictureId));
            Assert.True(await db.Pictures.CountAsync(TestContext.Current.CancellationToken) > 0);
        }
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = $"demo-{Guid.NewGuid():N}",
            Email = $"demo-{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user);
        Assert.True(result.Succeeded);
        return user;
    }

    public void Dispose()
    {
        var env = _factory.Services.GetService<IWebHostEnvironment>();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        TempDirectoryCleanup.DeleteFileBestEffort(_dbPath);
        if (env is not null)
            TempDirectoryCleanup.DeleteBestEffort(Path.Combine(env.ContentRootPath, "Data", "DemoMedia"));
    }
}
