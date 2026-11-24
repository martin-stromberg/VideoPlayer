using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using VideoWebPlayer.Tests.Infrastructure.Testing;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// E2E-Tests für den Demodatenbestand auf dem Registrierungsweg des ersten Benutzers:
/// Die Auswahl im bestehenden Registrierungsdialog füllt die Bibliothek,
/// der leere Pfad lässt sie unberührt — gegen echten Host, echte SQLite-DB und echtes Dateisystem.
/// </summary>
[Trait("Category", "E2E")]
[OsInterface]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class DemoDataRegistrationE2ETests : IDisposable
{
    private const string DemoDataSetId = "Demobibliothek";

    private readonly string _dbPath;
    private readonly WebApplicationFactory<global::Program> _factory;

    public DemoDataRegistrationE2ETests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vwp-demoreg-{Guid.NewGuid()}.db");
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
    public async Task RegisterPage_FirstUser_ShowsDemoDataSetOption()
    {
        using var client = CreateClient();

        var (registerHtml, _, _) = await GetRegisterFormAsync(client);

        Assert.Contains("demo-data-set", registerHtml);
        Assert.Contains(DemoDataSetId, registerHtml);
    }

    [Fact]
    public async Task RegisterFirstUser_WithDemoDataSet_FillsLibrary()
    {
        using var client = CreateClient();
        var email = $"demoreg-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email, DemoDataSetId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = await db.MediaSources.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(MediaSourceType.LocalDirectory, source.SourceType);
        Assert.True(Path.IsPathRooted(source.Path));
        Assert.True(Directory.Exists(source.Path));

        Assert.Equal(1, await db.MediaSourceUsers.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.Movies.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.TVShows.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await db.TVShowEpisodes.CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await db.Pictures.CountAsync(TestContext.Current.CancellationToken) > 0);
    }

    [Fact]
    public async Task RegisterFirstUser_WithoutDemoDataSet_StaysEmpty()
    {
        using var client = CreateClient();
        var email = $"nodemo-{Guid.NewGuid():N}@example.com";

        await RegisterAsync(client, email, null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(0, await db.MediaSources.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.Movies.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await db.TVShows.CountAsync(TestContext.Current.CancellationToken));
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    private async Task<(string Html, string Token, string CookieHeader)> GetRegisterFormAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync("/Account/Register", TestContext.Current.CancellationToken);
        var html = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("_handler", html, StringComparison.OrdinalIgnoreCase);

        var token = ExtractAntiforgeryToken(html);
        var cookieHeader = string.Join("; ", getResponse.Headers
            .Where(h => string.Equals(h.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value ?? Enumerable.Empty<string>())
            .Select(value => value.Split(';', 2)[0]));
        Assert.False(string.IsNullOrWhiteSpace(cookieHeader));

        return (html, token, cookieHeader);
    }

    private async Task RegisterAsync(HttpClient client, string email, string? demoDataSetId)
    {
        var (_, token, cookieHeader) = await GetRegisterFormAsync(client);

        var form = new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "DemoReg-123",
            ["Input.ConfirmPassword"] = "DemoReg-123",
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = token
        };
        if (demoDataSetId is not null)
            form["Input.DemoDataSetId"] = demoDataSetId;

        var postResponse = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(form), TestContext.Current.CancellationToken);
        var postBody = await postResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            postResponse.StatusCode == HttpStatusCode.Redirect,
            $"Status {postResponse.StatusCode}. Body-Ausschnitt: {postBody[..Math.Min(postBody.Length, 600)]}");
        Assert.Contains("/Account/RegisterConfirmation", postResponse.Headers.Location?.OriginalString ?? string.Empty);
        Assert.False(postBody.Contains("Beim Anlegen des Demodatenbestands", StringComparison.OrdinalIgnoreCase));
        Assert.False(postBody.Contains("Fehler", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        foreach (System.Text.RegularExpressions.Match input in System.Text.RegularExpressions.Regex.Matches(html, "<input\\b[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var name = System.Text.RegularExpressions.Regex.Match(input.Value, "\\bname=\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!name.Success || !name.Groups[1].Value.Equals("__RequestVerificationToken", StringComparison.Ordinal))
                continue;

            var value = System.Text.RegularExpressions.Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Assert.True(value.Success, "Der Antiforgery-Token besitzt keinen Wert.");
            return WebUtility.HtmlDecode(value.Groups[1].Value);
        }

        Assert.Fail("Antiforgery-Token wurde im Registrierungsformular nicht gefunden.");
        return string.Empty;
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
