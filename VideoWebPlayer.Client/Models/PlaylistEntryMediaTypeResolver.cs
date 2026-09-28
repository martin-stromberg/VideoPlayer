namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Resolves a playlist entry's <see cref="MediaTypeValues"/> value to the representations needed for
    /// playback: the player-facing media type expected by <c>VideoPlayer.razor</c>, and the <c>{type}</c>
    /// path segment used by the <c>/api/items/{type}/{id}/stream</c> and <c>/api/items/{type}/{id}/download</c>
    /// endpoints. Shared by <c>PlaylistService</c> (server-side stream URL / start-playback resolution) and
    /// <c>VideoPlayer.razor</c> (client-side resolution when applying a Next/Previous/Advance/Restart result).
    /// </summary>
    public static class PlaylistEntryMediaTypeResolver
    {
        /// <summary>
        /// Resolves the <c>MediaType</c> value expected by <c>VideoPlayer.razor</c> (<c>"movie"</c> or
        /// <c>"episode"</c>), matching the convention already used by <c>MovieCollectionDetails.razor</c> and
        /// <c>TVShowDetails.razor</c>.
        /// </summary>
        /// <param name="entryMediaType">The playlist entry's media type.</param>
        /// <returns><c>"episode"</c> or <c>"movie"</c>.</returns>
        public static string ToPlayerMediaType(string entryMediaType)
            => string.Equals(entryMediaType, MediaTypeValues.TVShowEpisode, StringComparison.OrdinalIgnoreCase) ? "episode" : "movie";

        /// <summary>
        /// Resolves the <c>{type}</c> path segment used by the <c>/api/items/{type}/{id}/stream</c> and
        /// <c>/api/items/{type}/{id}/download</c> endpoints for the given playlist entry media type.
        /// </summary>
        /// <param name="entryMediaType">The playlist entry's media type.</param>
        /// <returns><c>"tvshowepisode"</c> or <c>"movie"</c>.</returns>
        public static string ResolveItemStreamType(string entryMediaType)
            => string.Equals(entryMediaType, MediaTypeValues.TVShowEpisode, StringComparison.OrdinalIgnoreCase) ? "tvshowepisode" : "movie";

        /// <summary>
        /// Whether the given media type can be directly played back (<c>Movie</c> or <c>TVShowEpisode</c>);
        /// collection entries (<c>TVShow</c>, <c>TVShowSeason</c>, <c>MovieCollection</c>) are not. Shared by
        /// <c>PlaylistService</c> (playback-navigation filtering) and <c>PlaylistEntriesList.razor</c>
        /// (whether to show the "Abspielen" button for an entry).
        /// </summary>
        /// <param name="mediaType">The media type to check.</param>
        /// <returns><see langword="true"/> if the media type is directly playable.</returns>
        public static bool IsPlayable(string mediaType)
            => string.Equals(mediaType, MediaTypeValues.Movie, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, MediaTypeValues.TVShowEpisode, StringComparison.OrdinalIgnoreCase);
    }
}
