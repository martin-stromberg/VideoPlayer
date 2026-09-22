using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Test auf HTTP-Ebene: Eine lokale Medienquelle wird im laufenden Host gescannt
/// und klassifiziert; die klassifizierte TVShow ist anschließend über die API abrufbar.
/// </summary>
[Trait("Category", "E2E")]
public sealed class LocalMediaSourcePipelineE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _rootDir;
    private readonly WebApplicationFactory<global::Program> _factory;

    public LocalMediaSourcePipelineE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-local-pipe-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        _rootDir = Path.Combine(Path.GetTempPath(), $"vwp-local-pipe-{Guid.NewGuid():N}");
        var showDir = Path.Combine(_rootDir, "PipelineShow");
        Directory.CreateDirectory(showDir);
        File.WriteAllText(Path.Combine(showDir, "episode01.mkv"), "video");
        File.WriteAllText(Path.Combine(showDir, "tvshow.nfo"),
            "<tvshow><title>Lokale E2E Show</title><genre>Drama</genre></tvshow>");
        File.WriteAllText(Path.Combine(showDir, "episode01.nfo"),
            "<episodedetails><title>Pilot</title><season>1</season><episode>1</episode></episodedetails>");

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
    public async Task LocalSource_ScanAndClassify_ExposesTvShowViaApi()
    {
        var ct = TestContext.Current.CancellationToken;
        long sourceId;
        string token;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tokenService = scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>();

            var user = new ApplicationUser
            {
                UserName = $"viewer-{Guid.NewGuid():N}",
                Email = $"viewer-{Guid.NewGuid():N}@example.com",
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user);
            Assert.True(createResult.Succeeded);

            var source = new MediaSource
            {
                Name = "Lokale Pipeline-Quelle",
                Path = _rootDir,
                SourceType = MediaSourceType.LocalDirectory,
                Host = string.Empty,
                Port = 0,
                CreatedAt = DateTime.UtcNow
            };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync(ct);
            sourceId = source.Id;

            db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });
            await db.SaveChangesAsync(ct);

            token = tokenService.CreateToken(user).token;
        }

        // Scan + Klassifizierung über die echten Dienste des Hosts.
        using (var scope = _factory.Services.CreateScope())
        {
            var scanner = scope.ServiceProvider.GetRequiredService<MediaSourceScanner>();
            var classifier = scope.ServiceProvider.GetRequiredService<MediaSourceClassifier>();

            await scanner.ScanAllSourcesAsync(ct);
            while (await scanner.ScanNextMediaCollection(ct)) { }
            await classifier.ClassifyAllAsync(ct);
        }

        long showId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var show = await db.TVShows.SingleOrDefaultAsync(s => s.MediaSourceId == sourceId, ct);
            Assert.NotNull(show);
            Assert.Equal("Lokale E2E Show", show!.Name);
            Assert.Single(await db.TVShowEpisodes.Where(e => e.MediaSourceId == sourceId).ToListAsync(ct));
            showId = show.Id;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/api/items/tvshow/{showId}?access_token={token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<DtoTVShow>(cancellationToken: ct);
        Assert.NotNull(dto);
        Assert.Equal("Lokale E2E Show", dto!.Name);
        Assert.NotNull(dto.Seasons);
        Assert.Single(dto.Seasons!);
        Assert.Single(dto.Seasons![0].Episodes!);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        try { Directory.Delete(_rootDir, recursive: true); } catch { /* ignore */ }
    }
}
