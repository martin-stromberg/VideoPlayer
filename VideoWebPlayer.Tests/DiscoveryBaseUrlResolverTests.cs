using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DiscoveryBaseUrlResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsAdminBaseUrl_WhenSet()
    {
        // Der Admin-Override wird pro Anfrage in einem frischen Scope gelesen und
        // schlägt die Operator-Konfiguration.
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:discovery-resolver-admin?mode=memory&cache=shared");
        await connection.OpenAsync(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<ProgramSettingsService>();
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
            await scope.ServiceProvider.GetRequiredService<ProgramSettingsService>()
                .UpdateGeneralSettingsAsync(
                    new GeneralSettingsUpdate("Titel", 60, 7, 30, true, "https://videos.example.com/videoplayer/"),
                    ct);
        }

        var resolver = CreateResolver(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DiscoveryOptions { PublicBaseUrl = "http://configured.example/" },
            CreateServer());

        var url = await resolver.ResolveAsync(ct);

        Assert.Equal("https://videos.example.com/videoplayer/", url);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToConfiguredUrl_AndLogsWarning_WhenSettingsReadFails()
    {
        // Fail-open: ein DB-Fehler darf die Discovery-Präsenz nicht kippen, wird aber
        // als Warnung protokolliert statt still geschluckt.
        var ct = TestContext.Current.CancellationToken;
        var messages = new ConcurrentQueue<string>();

        var resolver = CreateResolver(
            ThrowingScopeFactory(),
            new DiscoveryOptions { PublicBaseUrl = "https://configured.example/app/" },
            CreateServer(),
            new ListLogger<DiscoveryBaseUrlResolver>(messages));

        var url = await resolver.ResolveAsync(ct);

        Assert.Equal("https://configured.example/app/", url);
        Assert.Contains(messages, m => m.Contains("Admin-Basis-URL konnte nicht gelesen werden"));
    }

    [Fact]
    public async Task ResolveAsync_UsesBoundAddress_WhenNothingConfigured()
    {
        // „Nichts konfiguriert": die Settings-Abfrage läuft erfolgreich, der
        // Admin-Override ist aber leer (null) — die Ableitung greift.
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:discovery-resolver-none?mode=memory&cache=shared");
        await connection.OpenAsync(ct);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<ProgramSettingsService>();
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(ct);
        }

        var features = new FeatureCollection();
        var addresses = new ServerAddressesFeature();
        addresses.Addresses.Add("http://192.168.1.5:5000");
        features.Set<IServerAddressesFeature>(addresses);

        var resolver = CreateResolver(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DiscoveryOptions(),
            CreateServer(features));

        var url = await resolver.ResolveAsync(ct);

        Assert.Equal("http://192.168.1.5:5000", url);
    }

    [Fact]
    public async Task ResolveAsync_UsesBoundAddress_WhenSettingsReadFails()
    {
        // Fail-open ohne konfigurierte URL: die Settings-Abfrage schlägt fehl, die
        // Ableitung über die gebundenen Adressen greift trotzdem.
        var ct = TestContext.Current.CancellationToken;

        var features = new FeatureCollection();
        var addresses = new ServerAddressesFeature();
        addresses.Addresses.Add("http://192.168.1.5:5000");
        features.Set<IServerAddressesFeature>(addresses);

        var resolver = CreateResolver(
            ThrowingScopeFactory(),
            new DiscoveryOptions(),
            CreateServer(features));

        var url = await resolver.ResolveAsync(ct);

        Assert.Equal("http://192.168.1.5:5000", url);
    }

    private static DiscoveryBaseUrlResolver CreateResolver(
        IServiceScopeFactory scopeFactory,
        DiscoveryOptions options,
        IServer server,
        ListLogger<DiscoveryBaseUrlResolver>? logger = null)
        => new(
            scopeFactory,
            Options.Create(options),
            server,
            new ConfigurationBuilder().Build(),
            logger ?? new ListLogger<DiscoveryBaseUrlResolver>(new ConcurrentQueue<string>()));

    private static IServer CreateServer(IFeatureCollection? features = null)
    {
        var server = new Mock<IServer>();
        server.SetupGet(s => s.Features).Returns(features ?? new FeatureCollection());
        return server.Object;
    }

    private static IServiceScopeFactory ThrowingScopeFactory()
    {
        // ProgramSettingsService ist nicht registriert — GetRequiredService wirft wie bei
        // einer defekten Scope-/DB-Verdrahtung. Ein gemockter Provider genügt; es gibt
        // keinen echten ServiceProvider, der disposed werden müsste.
        var serviceProvider = new Mock<IServiceProvider>();
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return scopeFactory.Object;
    }
}
