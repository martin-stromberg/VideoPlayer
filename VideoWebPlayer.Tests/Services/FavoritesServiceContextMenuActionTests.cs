using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests favorite removal for context menu actions.
/// </summary>
public class FavoritesServiceContextMenuActionTests : ContinueWatchingServiceTestBase
{
    public static TheoryData<FavoriteEntry> FavoriteTypes => new()
    {
        new FavoriteEntry { UserId = "unused", MovieCollectionId = 101 },
        new FavoriteEntry { UserId = "unused", TVShowId = 102 },
        new FavoriteEntry { UserId = "unused", TVShowSeasonId = 103 },
        new FavoriteEntry { UserId = "unused", TVShowEpisodeId = 104 },
        new FavoriteEntry { UserId = "unused", MovieId = 105 }
    };

    [Theory]
    [MemberData(nameof(FavoriteTypes))]
    public async Task RemoveFavoriteAsync_WithPersistenceId_RemovesEveryFavoriteType(FavoriteEntry targetTemplate)
    {
        var service = new FavoritesService(_db, _notificationService);
        var target = CopyFavorite(targetTemplate, _testUserId);
        var decoy = new FavoriteEntry { UserId = "other-user", MovieId = 999 };

        _db.FavoriteEntries.Add(target);
        _db.FavoriteEntries.Add(decoy);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.RemoveFavoriteAsync(_testUserId, new FavoriteEntry { UserId = string.Empty, Id = target.Id }, TestContext.Current.CancellationToken);

        Assert.False(await _db.FavoriteEntries.AnyAsync(f => f.Id == target.Id, TestContext.Current.CancellationToken));
        Assert.True(await _db.FavoriteEntries.AnyAsync(f => f.Id == decoy.Id, TestContext.Current.CancellationToken));
    }

    private static FavoriteEntry CopyFavorite(FavoriteEntry source, string userId)
        => new()
        {
            UserId = userId,
            MovieCollectionId = source.MovieCollectionId,
            TVShowId = source.TVShowId,
            TVShowSeasonId = source.TVShowSeasonId,
            TVShowEpisodeId = source.TVShowEpisodeId,
            MovieId = source.MovieId
        };
}
