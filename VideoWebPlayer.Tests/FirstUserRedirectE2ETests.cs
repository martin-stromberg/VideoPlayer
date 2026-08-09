using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Test für die automatische Weiterleitung zum Registrierungsformular,
/// wenn noch kein Benutzer existiert.
/// </summary>
public sealed class FirstUserRedirectE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public FirstUserRedirectE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-e2e-{Guid.NewGuid()}.db");
        try { File.Delete(_dbPath); } catch { /* ensure clean state */ }

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:ApiToken", "test-api-token");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
            });
    }

    [Fact]
    public async Task LoginPage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/Account/Login?ReturnUrl=%2Fadmin%2Fbackups");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("/Account/Register", location.AbsolutePath);

        var decodedQuery = System.Net.WebUtility.UrlDecode(location.Query);
        Assert.Contains("ReturnUrl=", decodedQuery);
        Assert.Contains("/admin/backups", decodedQuery);
    }

    [Fact]
    public async Task HomePage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("/Account/Register", location.AbsolutePath);

        var decodedQuery = System.Net.WebUtility.UrlDecode(location.Query);
        Assert.Contains("ReturnUrl=", decodedQuery);
        Assert.EndsWith("/", decodedQuery);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
