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
        Playlist = 4
    }
}
