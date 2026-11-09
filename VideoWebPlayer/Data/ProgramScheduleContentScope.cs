namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The content spectrum of the automated TV channel's program schedule — the admin setting
    /// that decides whether the viewer-release check of <see cref="VideoWebPlayer.Services.ProgramScheduleService"/>
    /// applies during planning. Persisted as <see cref="Setup.ProgramScheduleContentScope"/>
    /// (int column); <see cref="SharedOnly"/> must keep the value 0 so legacy backups and
    /// corrupted values fall back to the restrictive behaviour.
    /// </summary>
    public enum ProgramScheduleContentScope
    {
        /// <summary>
        /// Only contents released for every non-admin user are scheduled — via regular
        /// media-source access for all or an individual unlock of the owning show/movie
        /// collection for all (the previous behaviour; default).
        /// </summary>
        SharedOnly = 0,

        /// <summary>
        /// All contents the rules expand to are scheduled — the audience release check is
        /// ignored entirely, so viewers can see stream contents they may not access in the
        /// media library.
        /// </summary>
        AllContent = 1
    }
}
