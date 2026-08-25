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

    [Fact]
    public void ApiDocumentationContainsMauiRelevantRoutes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var apiDocument = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "API.md"));
        var requiredRoutes = new[]
        {
            "GET /api/health",
            "POST /api/auth/login",
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
            "GET /hubs/mediaupdate"
        };

        foreach (var route in requiredRoutes)
        {
            Assert.Contains(route, apiDocument, StringComparison.Ordinal);
        }
    }

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
