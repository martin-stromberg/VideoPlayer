using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> setup for pairing tests:
/// temp SQLite database, generated JWT key and the common test settings.
/// </summary>
internal static class PairingWebApplicationFactory
{
    /// <summary>Value of <c>Jwt:ApiToken:Maui</c>, the only gate key accepted by <c>/api/auth/login</c>.</summary>
    public const string MauiApiToken = "test-maui-api-token";

    /// <summary>Value of <c>Jwt:ApiToken:Web</c>, a gate key the Maui-only endpoints must reject.</summary>
    public const string WebApiToken = "test-web-api-token";

    /// <summary>Value of the legacy <c>Jwt:ApiToken</c>, a gate key the Maui-only endpoints must reject.</summary>
    public const string LegacyApiToken = "test-legacy-api-token";

    public static string CreateTempDbPath(string filePrefix)
        => Path.Combine(Path.GetTempPath(), $"{filePrefix}-{Guid.NewGuid()}.db");

    public static WebApplicationFactory<global::Program> Create(string dbPath, Action<IWebHostBuilder>? configure = null)
    {
        var jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(WebHostDefaults.EnvironmentKey, "Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath}");
                builder.UseSetting("Jwt:Key", jwtKey);
                builder.UseSetting("Jwt:Issuer", "VideoWebPlayer.Tests");
                builder.UseSetting("Jwt:ApiToken", LegacyApiToken);
                builder.UseSetting("Jwt:ApiToken:Web", WebApiToken);
                builder.UseSetting("Jwt:ApiToken:Maui", MauiApiToken);
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
                configure?.Invoke(builder);
            });
    }
}
