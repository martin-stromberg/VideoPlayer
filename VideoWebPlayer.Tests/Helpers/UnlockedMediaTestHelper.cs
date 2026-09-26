using System;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared construction logic for <see cref="UnlockedMediaEntry"/> test fixtures, used by both
/// <see cref="PlaylistServiceTestBase"/> and <see cref="PlaylistsE2ETestBase"/> to grant a user
/// unlocked access to a movie collection or TV show directly in the database.
/// </summary>
public static class UnlockedMediaTestHelper
{
    public static UnlockedMediaEntry CreateUnlockedMediaEntry(string userId, string mediaType, long mediaId) => new()
    {
        UserId = userId,
        MovieCollectionId = mediaType == MediaTypeValues.MovieCollection ? mediaId : null,
        TVShowId = mediaType == MediaTypeValues.TVShow ? mediaId : null,
        CreatedAt = DateTime.UtcNow
    };
}
