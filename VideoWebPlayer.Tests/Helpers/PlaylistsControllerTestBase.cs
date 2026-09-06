using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Provides a <see cref="PlaylistsController"/> backed by a real SQLite in-memory database and
/// a real <see cref="PlaylistService"/> for controller-level tests.
/// </summary>
public abstract class PlaylistsControllerTestBase : IDisposable
{
    protected readonly ApplicationUser _user;
    protected readonly ApplicationUser _otherUser;
    protected readonly FakeAuthService _fakeAuth;
    protected readonly ApplicationDbContext _db;
    protected PlaylistsController _controller;
    private readonly SqliteConnection _keeperConnection;
    private readonly IServiceProvider _serviceProvider;

    protected PlaylistsControllerTestBase()
    {
        var connectionString = $"Data Source=file:playlists-{Guid.NewGuid()}?mode=memory&cache=shared";
        _keeperConnection = new SqliteConnection(connectionString);
        _keeperConnection.Open();

        _user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "playlist-user@test.com" };
        _otherUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "other-playlist-user@test.com" };
        _fakeAuth = new FakeAuthService { CurrentUser = _user };

        var services = new ServiceCollection();
        services.AddSingleton<EventManager>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        _serviceProvider = services.BuildServiceProvider();

        var scope = _serviceProvider.CreateScope();
        _db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _db.Database.EnsureCreated();

        _db.Users.AddRange(_user, _otherUser);
        _db.SaveChanges();

        _controller = CreateController(new PlaylistSettings());
    }

    protected PlaylistsController CreateController(PlaylistSettings playlistSettings)
    {
        var options = Options.Create(playlistSettings);
        var unlockedMediaService = new UnlockedMediaService(_db, _fakeAuth);
        var playlistService = new PlaylistService(_db, unlockedMediaService, options);

        return new PlaylistsController(playlistService, _fakeAuth, NullLogger<PlaylistsController>.Instance, options)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    public void Dispose()
    {
        _keeperConnection.Dispose();
    }
}
