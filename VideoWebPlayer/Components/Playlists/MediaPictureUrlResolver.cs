namespace VideoWebPlayer.Components.Playlists;

/// <summary>
/// Builds the "/api/pictures/{id}" thumbnail URL (with the access token as a query parameter, since an
/// <c>&lt;img&gt;</c> tag cannot send an Authorization header) for a nullable picture id, or the
/// placeholder image path when no picture id is set. Shared by <c>MediaSearchSelector</c> and
/// <c>PlaylistEntriesList</c> so both render their media thumbnails identically instead of each
/// duplicating the same URL-building logic.
/// </summary>
public static class MediaPictureUrlResolver
{
    /// <summary>
    /// Resolves the thumbnail URL for <paramref name="pictureId"/>, or the placeholder image path if it
    /// is <see langword="null"/>.
    /// </summary>
    /// <param name="pictureId">The picture id, or <see langword="null"/> if none is set.</param>
    /// <param name="authorizationToken">The current user's bearer token, sent as the access_token query parameter.</param>
    /// <returns>The resolved image URL.</returns>
    public static string Resolve(long? pictureId, string? authorizationToken)
        => pictureId.HasValue
            ? $"/api/pictures/{pictureId}?access_token={authorizationToken}"
            : "/images/placeholder.png";
}
