namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The kind of content a <see cref="ProgramRuleCursor"/> refers to via
    /// <see cref="ProgramRuleCursor.LastContentId"/>.
    /// </summary>
    public enum ProgramCursorContentType
    {
        /// <summary>A movie (<see cref="Movie"/>).</summary>
        Movie = 0,

        /// <summary>A TV show episode (<see cref="TVShowEpisode"/>).</summary>
        TVShowEpisode = 1,

        /// <summary>A playlist entry (<see cref="PlaylistEntry"/>) of a playlist material.</summary>
        PlaylistEntry = 2
    }
}
