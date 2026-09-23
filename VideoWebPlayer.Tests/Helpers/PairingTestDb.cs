using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared in-memory SQLite database fixture for pairing/device-token service tests.
/// </summary>
internal sealed class PairingTestDb : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    private PairingTestDb(SqliteConnection connection, ServiceProvider provider, IServiceScope scope, ApplicationDbContext db)
    {
        Connection = connection;
        _provider = provider;
        _scope = scope;
        Db = db;
    }

    public SqliteConnection Connection { get; }
    public ApplicationDbContext Db { get; }

    public IServiceScope CreateScope() => _provider.CreateScope();

    public PairingService CreatePairingService() => CreatePairingService(Db);

    public PairingService CreatePairingService(ApplicationDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        return new PairingService(db, new DeviceTokenService(db), configuration);
    }

    public static async Task<PairingTestDb> CreateAsync(string dbName, CancellationToken ct, string? extraConnectionStringPart = null)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared{extraConnectionStringPart}";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        var serviceProvider = services.BuildServiceProvider();

        var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        return new PairingTestDb(connection, serviceProvider, scope, db);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
        Connection.Dispose();
    }
}
