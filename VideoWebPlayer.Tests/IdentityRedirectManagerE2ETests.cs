using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VideoWebPlayer.Tests;

[Trait("Category", "E2E")]
public sealed class IdentityRedirectManagerE2ETests : IDisposable
{
    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public IdentityRedirectManagerE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-redirect-{Guid.NewGuid()}.db");
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
    public async Task RegisterWithConfirmedAccount_DoesNotThrowNavigationException()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var registerUrl = "/Account/Register?ReturnUrl=%2F";
        var registerResponse = await client.GetAsync(registerUrl, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var html = await registerResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        var form = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [ExtractInputName(html, "Input.Email")] = $"redirect-{Guid.NewGuid()}@example.com",
            [ExtractInputName(html, "Input.Password")] = "Test123!",
            [ExtractInputName(html, "Input.ConfirmPassword")] = "Test123!",
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = ExtractInputValue(html, "__RequestVerificationToken")
        });
        var navigationExceptions = new ConcurrentQueue<string>();

        void OnFirstChanceException(object? _, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception is NavigationException
                && args.Exception.StackTrace?.Contains(
                    "IdentityRedirectManager",
                    StringComparison.Ordinal) == true)
            {
                navigationExceptions.Enqueue(args.Exception.StackTrace);
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        HttpResponseMessage postResponse;
        try
        {
            postResponse = await client.PostAsync(registerUrl, form, TestContext.Current.CancellationToken);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
        }

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);
        Assert.Equal("/Account/RegisterConfirmation", GetPath(postResponse.Headers.Location!));
        Assert.Contains("returnUrl=%2F", postResponse.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(navigationExceptions);
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

    private static string GetPath(Uri location)
    {
        var path = location.IsAbsoluteUri
            ? location.AbsolutePath
            : location.OriginalString.Split('?', 2)[0];
        return path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
    }

    public void Dispose()
    {
        _factory.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }
}
