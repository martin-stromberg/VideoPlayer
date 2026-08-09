using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task RegisterFirstUser_SubmitsAndRedirectsToHome()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        // 1. Startseite -> Weiterleitung auf Registrierung
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        var registerUrl = homeResponse.Headers.Location;
        Assert.NotNull(registerUrl);
        Assert.Equal("/Account/Register", registerUrl.AbsolutePath);

        // 2. Registrierungsseite laden und Antiforgery-Token extrahieren
        var registerResponse = await client.GetAsync(registerUrl);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var html = await registerResponse.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token not found in register form");
        var token = match.Groups[1].Value;

        // 3. Registrierungsformular absenden
        var form = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["Input.Email"] = $"e2e-{Guid.NewGuid()}@example.com",
            ["Input.Password"] = "Test123!",
            ["Input.ConfirmPassword"] = "Test123!",
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = token,
        });

        var postResponse = await client.PostAsync(registerUrl, form);
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        var afterRegister = postResponse.Headers.Location;
        Assert.NotNull(afterRegister);
        Assert.Equal("/Account/RegisterConfirmation", afterRegister.AbsolutePath);

        var decodedQuery = System.Net.WebUtility.UrlDecode(afterRegister.Query);
        Assert.Contains("returnUrl=", decodedQuery);
        Assert.EndsWith("/", decodedQuery);
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
