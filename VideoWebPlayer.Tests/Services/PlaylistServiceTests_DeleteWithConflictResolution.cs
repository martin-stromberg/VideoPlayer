using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;
using VideoWebPlayer.Hubs;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Regression tests for the "playlist deletion collides with a competing playlist-less continue-watching
/// entry" bug (Weiterschauen mit Playlist-Bezug, Schritt 6 Nachbesserung, Problem 3):
/// <see cref="PlaylistService.DeletePlaylistAsync"/> used to rely blindly on the database's
/// <c>ON DELETE SET NULL</c> foreign-key action when a playlist is deleted, which fails with a UNIQUE
/// constraint violation once a playlist-less <see cref="ContinueWatchingEntry"/> for the same media
/// already exists (both rows would end up with <c>PlaylistId = NULL</c>). Deliberately runs against the
/// real SQLite database <see cref="PlaylistServiceTestBase"/> already uses (unlike
/// <c>ContinueWatchingServiceTestBase</c>'s EF-InMemory provider, which enforces neither unique indexes
/// nor foreign-key actions and therefore cannot reproduce this failure) so the UNIQUE constraint violation
/// - and its fix - are both actually exercised.
/// </summary>
public class PlaylistServiceTests_DeleteWithConflictResolution : PlaylistServiceTestBase
{
    [Fact]
    public async Task DeletePlaylist_WithCompetingNullPlaylistEntry_ResolvesConflictAndDeletesPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 42");
        var service = CreatePlaylistServiceWithConflictResolution();
        var playlist = await service.CreatePlaylistAsync(_testUserId, "Zu Loeschen", null, null, ct);

        var boundEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        };
        var freeEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = null,
            Position = TimeSpan.FromSeconds(100),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 2
        };
        _db.ContinueWatchingEntries.AddRange(boundEntry, freeEntry);
        await _db.SaveChangesAsync(ct);

        // Vor der Behebung schlaegt dieser Aufruf mit einer DbUpdateException ("UNIQUE constraint failed")
        // fehl, weil die Datenbank beim Loeschen der Playlist versucht, boundEntry.PlaylistId per
        // ON DELETE SET NULL auf NULL zu setzen, was mit freeEntry kollidiert.
        await service.DeletePlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.False(await _db.Playlists.AnyAsync(p => p.Id == playlist.Id, ct));

        // Der playlist-gebundene Eintrag wurde entfernt (Konfliktaufloesung), nicht auf NULL gesetzt.
        Assert.False(await _db.ContinueWatchingEntries.AnyAsync(e => e.Id == boundEntry.Id, ct));

        // Der urspruenglich playlist-lose Eintrag bleibt unveraendert.
        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == freeEntry.Id, ct);
        Assert.Null(remaining.PlaylistId);
        Assert.Equal(TimeSpan.FromSeconds(100), remaining.Position);
    }

    [Fact]
    public async Task DeletePlaylist_WithoutCompetingNullPlaylistEntry_KeepsEntryToBeSetNullByDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie, "Film 43");
        var service = CreatePlaylistServiceWithConflictResolution();
        var playlist = await service.CreatePlaylistAsync(_testUserId, "Zu Loeschen Ohne Konflikt", null, null, ct);

        var boundEntry = new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movieId,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        };
        _db.ContinueWatchingEntries.Add(boundEntry);
        await _db.SaveChangesAsync(ct);

        await service.DeletePlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.False(await _db.Playlists.AnyAsync(p => p.Id == playlist.Id, ct));

        var remaining = await _db.ContinueWatchingEntries.AsNoTracking().SingleAsync(e => e.Id == boundEntry.Id, ct);
        Assert.Null(remaining.PlaylistId);
        Assert.Equal(TimeSpan.FromSeconds(300), remaining.Position);
    }

    /// <summary>
    /// Builds a <see cref="PlaylistService"/> wired to a real <see cref="ContinueWatchingService"/> (via a
    /// minimal <see cref="IServiceProvider"/>, mirroring how the production DI container resolves it lazily
    /// to avoid the <see cref="PlaylistService"/>-&gt;<see cref="ContinueWatchingService"/>-&gt;<see cref="IPlaylistService"/>
    /// circular dependency), so <see cref="PlaylistService.DeletePlaylistAsync"/> actually exercises
    /// <see cref="ContinueWatchingService.ResolvePlaylistDeletionConflictsAsync"/> against the shared,
    /// real-SQLite-backed <see cref="PlaylistServiceTestBase._db"/>. <see cref="_service"/> (built by the
    /// base class without a service provider) intentionally does not perform this conflict resolution, so
    /// callers of this method get a separate, dedicated instance instead.
    /// </summary>
    /// <returns>The wired <see cref="PlaylistService"/> instance.</returns>
    private PlaylistService CreatePlaylistServiceWithConflictResolution()
    {
        PlaylistService? playlistService = null;

        var services = new ServiceCollection();
        services.AddSingleton(_ => BuildContinueWatchingService(() => playlistService!));
        var serviceProvider = services.BuildServiceProvider();

        playlistService = CreateService(serviceProvider: serviceProvider);
        return playlistService;
    }

    private ContinueWatchingService BuildContinueWatchingService(Func<IPlaylistService> resolvePlaylistService)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = _testUserId, UserName = _testUserId });

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
    /// returns, resolved lazily on each call rather than once up front: <see cref="ContinueWatchingService"/>
    /// needs an <see cref="IPlaylistService"/> at construction time, but the real <see cref="PlaylistService"/>
    /// instance (also needing this <see cref="ContinueWatchingService"/>, resolved lazily via
    /// <see cref="IServiceProvider"/>) is only assigned after this constructor call returns.
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

        public Task RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken cancellationToken = default)
            => _resolve().RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken);

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
    }
}
