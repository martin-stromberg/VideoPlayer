using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Provides an in-memory-database-backed <see cref="PlaylistService"/> instance for tests.
/// </summary>
public abstract class PlaylistServiceTestBase
{
    protected readonly ApplicationDbContext _db;
    protected readonly EventManager _eventManager;
    protected readonly PlaylistService _service;
    protected readonly string _testUserId = "test-user-123";
    protected readonly string _otherUserId = "other-user-456";

    protected PlaylistServiceTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _eventManager = new EventManager();
        _db = new ApplicationDbContext(options, _eventManager);
        _service = CreateService(null);
    }

    protected PlaylistService CreateService(int? maxPlaylistsPerUser)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new PlaylistSettings { MaxPlaylistsPerUser = maxPlaylistsPerUser });
        return new PlaylistService(_db, _eventManager, settings);
    }
}
