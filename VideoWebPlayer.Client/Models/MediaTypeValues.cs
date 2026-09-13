namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Central string constants for the media types supported as playlist entries, mirroring
    /// <c>VideoWebPlayer.Data.MediaType</c>. Used at the client/server JSON boundary and in
    /// cascade/validation logic to avoid duplicating the raw literal values across DTOs, services
    /// and Razor components.
    /// </summary>
    public static class MediaTypeValues
    {
        /// <summary>
        /// A single movie.
        /// </summary>
        public const string Movie = "Movie";

        /// <summary>
        /// A single episode of a TV show.
        /// </summary>
        public const string TVShowEpisode = "TVShowEpisode";

        /// <summary>
        /// A season of a TV show.
        /// </summary>
        public const string TVShowSeason = "TVShowSeason";

        /// <summary>
        /// A whole TV show.
        /// </summary>
        public const string TVShow = "TVShow";

        /// <summary>
        /// A collection of movies.
        /// </summary>
        public const string MovieCollection = "MovieCollection";
    }
}
