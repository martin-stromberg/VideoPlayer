using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Controllers;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services;
using Xunit;

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

    protected async Task<long> CreateMovieAsync(string name = "Testfilm")
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    protected async Task<long> CreatePlaylistAsync(string name = "Meine Playlist")
    {
        var createResult = await _controller.CreatePlaylist(new DtoCreatePlaylistRequest { Name = name });
        var created = Assert.IsType<OkObjectResult>(createResult).Value as DtoPlaylist;
        return created!.Id;
    }

    /// <summary>
    /// Grants <see cref="_user"/> regular access to the media source with the given id (defaulting to the
    /// <c>MediaSourceId = 1</c> used by <see cref="CreateMovieAsync"/>) by inserting a
    /// <see cref="MediaSourceUser"/> entry, creating the referenced <see cref="MediaSource"/> first if it
    /// does not exist yet.
    /// </summary>
    /// <param name="mediaSourceId">The id of the media source to grant access to (created if missing).</param>
    protected async Task GrantMediaSourceAccessAsync(long mediaSourceId = 1)
    {
        if (!await _db.MediaSources.AnyAsync(s => s.Id == mediaSourceId))
        {
            _db.MediaSources.Add(new MediaSource { Id = mediaSourceId, Name = "Test Source", Path = "/test", Host = "localhost", Port = 22 });
            await _db.SaveChangesAsync();
        }

        _db.MediaSourceUsers.Add(new MediaSourceUser { UserId = _user.Id, MediaSourceId = mediaSourceId });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Rebuilds <see cref="_controller"/> with its <see cref="PlaylistService"/> wired to a real
    /// <see cref="ContinueWatchingService"/> (mirroring <c>PlaylistServiceTestBase.CreatePlaylistServiceWithConflictResolution</c>
    /// and production DI's lazy resolution via <see cref="IServiceProvider"/>, avoiding the
    /// <see cref="PlaylistService"/>-&gt;<see cref="ContinueWatchingService"/>-&gt;<see cref="IPlaylistService"/>
    /// circular dependency), so controller-level tests can exercise the Entwicklungsschritt-7
    /// Sicherheitsabfrage (409 Conflict via <see cref="ContinueWatchingConfirmationRequiredException"/>) end
    /// to end. The default <see cref="_controller"/> built by the constructor does not perform continue-
    /// watching conflict resolution at all, since most existing tests do not need it.
    /// </summary>
    protected void UseControllerWithContinueWatchingResolution()
    {
        PlaylistService? playlistService = null;

        var services = new ServiceCollection();
        services.AddSingleton(_ => BuildContinueWatchingService(() => playlistService!));
        var serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new PlaylistSettings());
        var unlockedMediaService = new UnlockedMediaService(_db, _fakeAuth);
        playlistService = new PlaylistService(_db, unlockedMediaService, options, serviceProvider);

        _controller = new PlaylistsController(playlistService, _fakeAuth, NullLogger<PlaylistsController>.Instance, options)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private ContinueWatchingService BuildContinueWatchingService(Func<IPlaylistService> resolvePlaylistService)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        var mockClientProxy = new Mock<IClientProxy>();
        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(x => x.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockClients.Setup(x => x.All).Returns(mockClientProxy.Object);
        var mockHubContext = new Mock<IHubContext<MediaUpdateHub>>();
        mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        var notificationService = new MediaUpdateNotificationService(mockHubContext.Object, Mock.Of<ILogger<MediaUpdateNotificationService>>());

        var programSettings = new ProgramSettingsService(_db, Mock.Of<ILogger<ProgramSettingsService>>());

        return new ContinueWatchingService(
            _db,
            userManagerMock.Object,
            Mock.Of<ILogger<ContinueWatchingService>>(),
            new ContinueWatchingBuffer(),
            notificationService,
            programSettings,
            new LazyPlaylistService(resolvePlaylistService));
    }

    /// <summary>
    /// Forwards every <see cref="IPlaylistService"/> call to the instance <paramref name="resolve"/>
    /// returns, resolved lazily on each call - see the identical helper and its remarks in
    /// <c>PlaylistServiceTestBase</c> for why this indirection is needed.
    /// </summary>
    private sealed class LazyPlaylistService : IPlaylistService
    {
        private readonly Func<IPlaylistService> _resolve;

        public LazyPlaylistService(Func<IPlaylistService> resolve) => _resolve = resolve;

        public Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistsAsync(userId, cancellationToken);

        public Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
            => _resolve().CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken);

        public Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)
            => _resolve().UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken);

        public Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().DeletePlaylistAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
            => _resolve().AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken);

        public Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, bool confirmContinueWatchingRemoval = false, CancellationToken cancellationToken = default)
            => _resolve().RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, confirmContinueWatchingRemoval, cancellationToken);

        public Task<DtoPlaylistEntry[]> GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistEntriesAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistEntriesPagedResult> GetPlaylistEntriesPagedAsync(long playlistId, string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => _resolve().GetPlaylistEntriesPagedAsync(playlistId, userId, pageNumber, pageSize, cancellationToken);

        public Task ReorderPlaylistEntryAsync(long playlistId, string userId, long entryId, long newSortOrder, CancellationToken cancellationToken = default)
            => _resolve().ReorderPlaylistEntryAsync(playlistId, userId, entryId, newSortOrder, cancellationToken);

        public Task<DtoPlaylistEntry[]> BatchReorderPlaylistEntriesAsync(long playlistId, string userId, List<(long EntryId, long NewSortOrder)> reorderOperations, CancellationToken cancellationToken = default)
            => _resolve().BatchReorderPlaylistEntriesAsync(playlistId, userId, reorderOperations, cancellationToken);

        public Task<long?> GetMaxSortOrderAsync(long playlistId, string userId, CancellationToken cancellationToken = default)
            => _resolve().GetMaxSortOrderAsync(playlistId, userId, cancellationToken);

        public Task<DtoPlaylistEntry> MoveEntryToBeginningAsync(long playlistId, string userId, long entryId, CancellationToken cancellationToken = default)
            => _resolve().MoveEntryToBeginningAsync(playlistId, userId, entryId, cancellationToken);

        public Task MoveEntryBetweenAsync(long playlistId, string userId, long entryId, long targetSortOrder, CancellationToken cancellationToken = default)
            => _resolve().MoveEntryBetweenAsync(playlistId, userId, entryId, targetSortOrder, cancellationToken);

        public Task<DtoPlaylist> ChangeSortModeAsync(long playlistId, string userId, string newSortMode, bool? confirmLossOfManualOrder, CancellationToken cancellationToken = default)
            => _resolve().ChangeSortModeAsync(playlistId, userId, newSortMode, confirmLossOfManualOrder, cancellationToken);

        public Task<DtoPlaylistNavigationResult?> GetNextPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
            => _resolve().GetNextPlaylistEntryAsync(playlistId, userId, currentEntryId, cancellationToken);

        public Task<DtoPlaylistNavigationResult?> GetPreviousPlaylistEntryAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
            => _resolve().GetPreviousPlaylistEntryAsync(playlistId, userId, currentEntryId, cancellationToken);

        public Task<DtoPlaylistNavigationResult?> AdvancePlaylistAsync(long playlistId, string userId, long currentEntryId, CancellationToken cancellationToken = default)
            => _resolve().AdvancePlaylistAsync(playlistId, userId, currentEntryId, cancellationToken);

        public Task<DtoPlaylistPlaybackStart> StartPlaylistAsync(long playlistId, string userId, long? entryId, CancellationToken cancellationToken = default)
            => _resolve().StartPlaylistAsync(playlistId, userId, entryId, cancellationToken);

        public Task ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(long mediaSourceId, CancellationToken cancellationToken = default)
            => _resolve().ResolvePlaylistBoundContinueWatchingReplacementsForSourceDeletionAsync(mediaSourceId, cancellationToken);
    }
}
