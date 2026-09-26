using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ApiDocumentationContractTests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public ApiDocumentationContractTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-api-contract-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { }

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:Issuer", "VideoWebPlayer.Tests");
                builder.UseSetting("Jwt:ApiToken", "test-legacy-api-token");
                builder.UseSetting("Jwt:ApiToken:Web", "test-web-api-token");
                builder.UseSetting("Jwt:ApiToken:Maui", "test-maui-api-token");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
            });
    }

    /// <summary>
    /// Every route <c>docs/API.md</c> must describe. Besides the long-standing Maui-relevant routes this
    /// covers the complete playlist area (management, entries, manual order, sort mode, genres, playback,
    /// cover) and the session endpoints of the QR bootstrap, so a route added to
    /// <c>PlaylistsController</c>/<c>AuthController</c> without documentation, or a documented route
    /// silently dropped from the document, fails the build.
    /// </summary>
    internal static readonly string[] RequiredRoutes =
    {
        "GET /api/health",
        "POST /api/auth/login",
        "POST /api/pairing/exchange",
        "POST /api/pairing/bootstrap",
        "POST /api/auth/refresh",
        "POST /api/auth/logout",
        "GET /api/Sources",
        "GET /api/SourceGenres/{sourceId}",
        "GET /api/items",
        "GET /api/items/recent",
        "GET /api/items/{type}/{id}",
        "GET /api/items/{type}/{id}/stream",
        "GET /api/pictures/{id}",
        "GET /api/sourceicons/{id}",
        "GET /api/favorites",
        "POST /api/favorites/toggle",
        "GET /api/continue-watching",
        "POST /api/continue-watching/progress",
        "POST /api/continue-watching/hide",
        "POST /api/continue-watching/skip",
        "GET /api/episodes/{episodeId}/background-image",
        "GET /api/playlists",
        "GET /api/playlists/public",
        "PUT /api/playlists/{id}/public",
        "GET /api/playlists/{id}",
        "POST /api/playlists",
        "PUT /api/playlists/{id}",
        "DELETE /api/playlists/{id}",
        "POST /api/playlists/{id}/entries",
        "DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}",
        "GET /api/playlists/{id}/entries",
        "GET /api/playlists/{id}/entries/paged",
        "PUT /api/playlists/{id}/entries/{entryId}/order",
        "POST /api/playlists/{id}/entries/batch-reorder",
        "GET /api/playlists/{id}/entries/max-sort-order",
        "POST /api/playlists/{id}/entries/{entryId}/move-to-beginning",
        "POST /api/playlists/{id}/entries/{entryId}/move-between",
        "PATCH /api/playlists/{id}/sort-mode",
        "PUT /api/playlists/{id}/genres",
        "POST /api/playlists/{id}/genres/reset",
        "POST /api/playlists/{id}/play",
        "POST /api/playlists/{id}/play/next",
        "POST /api/playlists/{id}/play/previous",
        "POST /api/playlists/{id}/play/advance",
        "POST /api/playlists/{id}/cover/upload",
        "POST /api/playlists/{id}/cover/regenerate",
        "POST /api/playlists/{id}/cover/preview",
        "GET /api/playlists/{id}/cover",
        "DELETE /api/playlists/{id}/cover",
        "GET /hubs/mediaupdate"
    };

    [Fact]
    public void ApiDocumentationContainsMauiRelevantRoutes()
    {
        AssertRequiredRoutesDocumented(ReadApiDocument());
    }

    /// <summary>
    /// Counter-proof for <see cref="ApiDocumentationContainsMauiRelevantRoutes"/>: for every required route,
    /// removing exactly that route from the document must make the check fail. Without this the check could
    /// silently degrade into one that passes no matter what the document contains.
    /// </summary>
    [Fact]
    public void ApiDocumentation_WithARequiredRouteRemoved_FailsTheContract()
    {
        var apiDocument = ReadApiDocument();

        foreach (var route in RequiredRoutes)
        {
            var mutilatedDocument = apiDocument.Replace(route, string.Empty, StringComparison.Ordinal);
            Assert.NotEqual(apiDocument, mutilatedDocument);
            Assert.ThrowsAny<Exception>(() => AssertRequiredRoutesDocumented(mutilatedDocument));
        }
    }

    /// <summary>
    /// Asserts that the given <c>docs/API.md</c> content mentions every route of
    /// <see cref="RequiredRoutes"/>.
    /// </summary>
    /// <param name="apiDocument">The content of <c>docs/API.md</c> to check.</param>
    internal static void AssertRequiredRoutesDocumented(string apiDocument)
    {
        foreach (var route in RequiredRoutes)
        {
            Assert.Contains(route, apiDocument, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Reads <c>docs/API.md</c> from the repository the test assembly was built in.
    /// </summary>
    /// <returns>The document's content.</returns>
    internal static string ReadApiDocument()
        => File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "API.md"));

    [Theory]
    [InlineData("test-legacy-api-token")]
    [InlineData("test-web-api-token")]
    public async Task MauiLogin_RejectsNonMauiApiTokens(string apiToken)
    {
        const string password = "ApiContract123!";
        var (email, _) = await CreateUserWithReadableMediaSourceAsync(password);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        client.DefaultRequestHeaders.Add("X-API-Key", apiToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthenticationRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds()
    {
        const string password = "ApiContract123!";
        var (email, sourceId) = await CreateUserWithReadableMediaSourceAsync(password);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var healthResponse = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal("OK", await healthResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        client.DefaultRequestHeaders.Add("X-API-Key", "test-maui-api-token");
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthenticationRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var token = await loginResponse.Content.ReadFromJsonAsync<AuthorizationToken>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(token?.token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.token);
        var itemsResponse = await client.GetAsync($"/api/items?mediaSourceId={sourceId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, itemsResponse.StatusCode);

        var json = await itemsResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("Contract Movie", item.GetProperty("title").GetString());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "VideoPlayer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private async Task<(string Email, long SourceId)> CreateUserWithReadableMediaSourceAsync(string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = $"api-contract-{Guid.NewGuid():N}",
            Email = $"api-contract-{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors.Select(error => error.Description)));

        var source = new MediaSource
        {
            Name = "Contract Source",
            Path = "/contract",
            Host = "localhost",
            Port = 22,
            CreatedAt = DateTime.UtcNow
        };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.MediaSourceUsers.Add(new MediaSourceUser { MediaSourceId = source.Id, UserId = user.Id });
        db.MovieCollections.Add(new MovieCollection
        {
            Name = "Contract Movie",
            MediaSourceId = source.Id,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (user.Email!, source.Id);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }
}
