namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Media types that can be referenced by a <see cref="PlaylistEntry"/>.
    /// </summary>
    public enum MediaType
    {
        /// <summary>
        /// A single movie.
        /// </summary>
        Movie,

        /// <summary>
        /// A single TV show episode.
        /// </summary>
        TVShowEpisode,

        /// <summary>
        /// A TV show season (cascades to its episodes when added to a playlist).
        /// </summary>
        TVShowSeason,

        /// <summary>
        /// A TV show (cascades to its seasons and episodes when added to a playlist).
        /// </summary>
        TVShow,

        /// <summary>
        /// A movie collection (cascades to its movies when added to a playlist).
        /// </summary>
        MovieCollection
    }
}
