namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The kind of program material referenced by a <see cref="ProgramRule"/> via
    /// <see cref="ProgramRule.MaterialType"/>/<see cref="ProgramRule.MaterialId"/> — exactly one
    /// material reference per rule, mirroring the polymorphic reference pattern of
    /// <see cref="PlaylistEntry.MediaType"/>/<see cref="PlaylistEntry.MediaId"/>.
    /// </summary>
    public enum ProgramRuleMaterialType
    {
        /// <summary>A whole TV show (series).</summary>
        TVShow = 0,

        /// <summary>A single movie.</summary>
        Movie = 1,

        /// <summary>A movie collection.</summary>
        MovieCollection = 2,

        /// <summary>A genre (deduplicated by name across media sources).</summary>
        Genre = 3,

        /// <summary>An existing user playlist.</summary>
        Playlist = 4,

        /// <summary>
        /// Trailers of movies scheduled in the upcoming days — a global material without a
        /// <see cref="ProgramRule.MaterialId"/> reference (filler material "trailer of upcoming
        /// movies").
        /// </summary>
        Trailer = 5,

        /// <summary>
        /// Unclassified video <see cref="MediaItem"/>s of a media source (clip source —
        /// <see cref="ProgramRule.MaterialId"/> references <c>MediaSources.Id</c>): short
        /// interstitial/mascot clips that were never classified as a movie or episode (files
        /// without an NFO), e.g. a dedicated "Maskottchen" media source.
        /// </summary>
        MediaSourceClips = 6
    }
}
