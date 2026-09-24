using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

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

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? settings = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

    public RefreshTokenService CreateRefreshTokenService(ApplicationDbContext? db = null, Dictionary<string, string?>? settings = null)
        => new(db ?? Db, CreateConfiguration(settings));

    public DeviceTokenService CreateDeviceTokenService(ApplicationDbContext? db = null)
    {
        var context = db ?? Db;
        return new DeviceTokenService(context, CreateRefreshTokenService(context));
    }

    public PairingService CreatePairingService() => CreatePairingService(Db);

    public PairingService CreatePairingService(ApplicationDbContext db)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        return new PairingService(db, CreateDeviceTokenService(db), configuration);
    }

    /// <summary>
    /// Erzeugt den Bootstrap-Service mit einem echten <see cref="UserManager{TUser}"/>
    /// und <see cref="AuthorizationTokenService"/> aus dem Fixture-Provider.
    /// </summary>
    public PairingBootstrapService CreatePairingBootstrapService(ApplicationDbContext? db = null, Dictionary<string, string?>? settings = null)
    {
        var context = db ?? Db;
        var scope = _provider.CreateScope();
        return new PairingBootstrapService(
            context,
            CreateDeviceTokenService(context),
            CreateRefreshTokenService(context, settings),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>(),
            CreateConfiguration(settings));
    }

    /// <summary>
    /// Legt einen Testbenutzer ueber den echten <see cref="UserManager{TUser}"/> an.
    /// </summary>
    public async Task<ApplicationUser> CreateUserAsync(string email, bool isAdmin = false)
    {
        var scope = _provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsAdmin = isAdmin
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Testbenutzer konnte nicht erstellt werden: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        return user;
    }

    public static async Task<PairingTestDb> CreateAsync(string dbName, CancellationToken ct, string? extraConnectionStringPart = null)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared{extraConnectionStringPart}";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        // Fuer Bootstrap-Service-Tests: echter UserManager + AuthorizationTokenService.
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "VideoWebPlayer.Tests"
            })
            .Build());
        services.AddSingleton(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)));
        services.AddScoped<AuthorizationTokenService>();
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
