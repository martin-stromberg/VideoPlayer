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
    public static string CreateTempDbPath(string filePrefix)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{filePrefix}-{Guid.NewGuid()}.db");
        try { File.Delete(dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* clean state is best-effort */ }
        return dbPath;
    }

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
                builder.UseSetting("Jwt:ApiToken", "test-legacy-api-token");
                builder.UseSetting("Jwt:ApiToken:Web", "test-web-api-token");
                builder.UseSetting("Jwt:ApiToken:Maui", "test-maui-api-token");
                builder.ConfigureServices(services =>
                {
                    services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = null);
                });
                configure?.Invoke(builder);
            });
    }
}
