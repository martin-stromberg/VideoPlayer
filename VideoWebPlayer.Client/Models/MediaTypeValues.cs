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
        public const string Movie = "Movie";
        public const string TVShowEpisode = "TVShowEpisode";
        public const string TVShowSeason = "TVShowSeason";
        public const string TVShow = "TVShow";
        public const string MovieCollection = "MovieCollection";
    }
}
