using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class AboutPageE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public AboutPageE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-about-{Guid.NewGuid()}.db");
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
    public async Task AboutPage_ReturnsFirstStepsAndGitHubLink()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/about");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Private Videobibliothek im Browser", html);
        Assert.Contains("Medienquelle", html);
        Assert.Contains("Scan und Klassifizierung", html);
        Assert.Contains("https://github.com/martin-stromberg/VideoPlayer", html);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }
}
