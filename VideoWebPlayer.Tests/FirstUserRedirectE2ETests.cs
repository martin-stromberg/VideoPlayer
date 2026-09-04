using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Test für die automatische Weiterleitung zum Registrierungsformular,
/// wenn noch kein Benutzer existiert.
/// </summary>
[Trait("Category", "E2E")]
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
                    services.Configure<IdentityOptions>(options => options.SignIn.RequireConfirmedAccount = false);
                });
            });
    }

    [Fact]
    public async Task LoginPage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/Account/Login?ReturnUrl=%2Fadmin%2Fbackups", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("/Account/Register", GetPath(location));

        var decodedQuery = System.Net.WebUtility.UrlDecode(GetQuery(location));
        Assert.Contains("ReturnUrl=", decodedQuery);
        Assert.Contains("/admin/backups", decodedQuery);
    }

    [Fact]
    public async Task HomePage_WithoutUsers_RedirectsToRegisterPreservingReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("/Account/Register", GetPath(location));

        var decodedQuery = System.Net.WebUtility.UrlDecode(GetQuery(location));
        Assert.Contains("ReturnUrl=", decodedQuery);
        Assert.EndsWith("/", decodedQuery);
    }

    [Fact]
    public async Task RegisterFirstUser_SubmitsAndSetsAuthCookie()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var homeResponse = await client.GetAsync("/", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, homeResponse.StatusCode);
        var registerUrl = homeResponse.Headers.Location;
        Assert.NotNull(registerUrl);
        Assert.Equal("/Account/Register", GetPath(registerUrl));

        var registerResponse = await client.GetAsync(registerUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var html = await registerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Input.Email", html);
        Assert.Contains("Input.Password", html);
        Assert.Contains("Input.ConfirmPassword", html);
        Assert.Contains("Create a new account", html);

        var antiforgeryToken = ExtractInputValue(html, "__RequestVerificationToken");
        var emailField = ExtractInputName(html, "Input.Email");
        var passwordField = ExtractInputName(html, "Input.Password");
        var confirmationField = ExtractInputName(html, "Input.ConfirmPassword");
        var email = $"e2e-{Guid.NewGuid()}@example.com";

        var form = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [emailField] = email,
            [passwordField] = "Test123!",
            [confirmationField] = "Test123!",
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = antiforgeryToken
        });

        var postResponse = await client.PostAsync(registerUrl, form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);
        Assert.Equal("/", GetPath(postResponse.Headers.Location!));
        Assert.True(
            postResponse.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders),
            "Die Registrierungsantwort enthält kein Set-Cookie-Header.");
        Assert.Contains(
            setCookieHeaders!,
            header => header.StartsWith("VideoWebPlayer.Auth=", StringComparison.Ordinal));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync(email));
    }

    private static string ExtractInputName(string html, string expectedName)
    {
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var name = Regex.Match(input.Value, "\\bname=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (name.Success && name.Groups[1].Value.Equals(expectedName, StringComparison.Ordinal))
                return name.Groups[1].Value;
        }

        Assert.Fail($"Das Formularfeld '{expectedName}' wurde im HTML nicht gefunden.");
        return string.Empty;
    }

    private static string GetPath(Uri location)
    {
        return location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?', 2)[0];
    }

    private static string GetQuery(Uri location)
    {
        return location.IsAbsoluteUri
            ? location.Query
            : location.OriginalString.Contains('?', StringComparison.Ordinal)
                ? location.OriginalString[location.OriginalString.IndexOf('?', StringComparison.Ordinal)..]
                : string.Empty;
    }

    private static string ExtractInputValue(string html, string expectedName)
    {
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            var name = Regex.Match(input.Value, "\\bname=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!name.Success || !name.Groups[1].Value.Equals(expectedName, StringComparison.Ordinal))
                continue;

            var value = Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            Assert.True(value.Success, $"Das Formularfeld '{expectedName}' besitzt keinen Wert.");
            return System.Net.WebUtility.HtmlDecode(value.Groups[1].Value);
        }

        Assert.Fail($"Das Formularfeld '{expectedName}' wurde im HTML nicht gefunden.");
        return string.Empty;
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
    }
}
