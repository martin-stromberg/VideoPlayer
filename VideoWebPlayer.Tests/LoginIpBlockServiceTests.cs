using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class LoginIpBlockServiceTests
{
    [Fact]
    public async Task Unblock_SubThresholdFailures_ResetsFailureCount()
    {
        // Regressionstest zu review-code.md: Unblock entfernte bisher nur persistierte
        // Sperren; im Cache liegende Fehlerzaehler unterhalb der Sperrschwelle blieben
        // bestehen. Unblock muss den Cache-Eintrag der IP immer entfernen, auch wenn
        // noch kein DB-Eintrag existiert.
        var ct = TestContext.Current.CancellationToken;
        var connectionString = "Data Source=file:ipblock-unblock?mode=memory&cache=shared";
        await using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync(ct);
        }

        var service = new LoginIpBlockService(provider.GetRequiredService<IServiceScopeFactory>());
        var ip = IPAddress.Parse("198.51.100.99");

        service.RegisterFailure(ip);
        service.RegisterFailure(ip);
        Assert.Equal(2, service.GetFailureCount(ip));
        Assert.False(service.IsBlocked(ip));

        var unblocked = service.Unblock(ip.ToString());

        Assert.False(unblocked); // keine persistierte Sperre vorhanden
        Assert.Equal(0, service.GetFailureCount(ip));
        Assert.False(service.IsBlocked(ip));
    }
}
