using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Builds a ready-to-use <see cref="ItemsController"/> against a fresh in-memory SQLite database,
/// centralizing the DI/EF Core scaffolding (<see cref="EventManager"/>, <see cref="ApplicationDbContext"/>,
/// <see cref="FakeAuthService"/>, <see cref="IUnlockedMediaService"/>, <see cref="RecentEntryService"/>)
/// that <c>ItemsControllerTests_Search</c> and <c>ItemsControllerAccessTests</c> previously duplicated
/// independently.
/// </summary>
public static class ItemsControllerTestFactory
{
    /// <summary>
    /// Creates the database, service provider and <see cref="ItemsController"/> instance, plus the
    /// <see cref="ApplicationUser"/> the controller is authenticated as.
    /// </summary>
    /// <param name="connectionString">
    /// The SQLite in-memory connection string, e.g. <c>Data Source=file:my-tests-{Guid.NewGuid()}?mode=memory&amp;cache=shared</c>.
    /// A shared-cache in-memory database is used, so a dedicated connection string per test (or per test
    /// class, if tests may run in parallel) keeps databases isolated.
    /// </param>
    /// <param name="userName">User name assigned to the seeded <see cref="ApplicationUser"/>.</param>
    /// <param name="logger">Logger passed to the controller; defaults to a no-op logger.</param>
    /// <param name="cancellationToken">Cancellation token for the database setup.</param>
    /// <returns>The db context, controller, and authenticated user.</returns>
    public static async Task<(ApplicationDbContext Db, ItemsController Controller, ApplicationUser User)> CreateAsync(
        string connectionString,
        string userName = "test-user@test.com",
        ILogger<ItemsController>? logger = null,
        CancellationToken cancellationToken = default)
    {
        // Keeps the shared-cache in-memory SQLite database alive for the lifetime of the returned
        // ApplicationDbContext instances; the connection is intentionally never disposed here, mirroring
        // the previous per-test-class setup this factory replaces.
        var keeperConnection = new SqliteConnection(connectionString);
        keeperConnection.Open();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = userName };
        var fakeAuth = new FakeAuthService { CurrentUser = user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IAuthService>(fakeAuth);
        services.AddScoped<IUnlockedMediaService, UnlockedMediaService>();

        var serviceProvider = services.BuildServiceProvider();
        var db = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var unlockedMediaService = serviceProvider.GetRequiredService<IUnlockedMediaService>();
        var recentEntryService = new RecentEntryService(db, fakeAuth, unlockedMediaService);
        var controller = new ItemsController(
            db,
            new SftpMediaSourceReader(),
            new MediaMetadataEditorService(db, null),
            recentEntryService,
            unlockedMediaService,
            fakeAuth,
            logger ?? NullLogger<ItemsController>.Instance);

        return (db, controller, user);
    }
}
