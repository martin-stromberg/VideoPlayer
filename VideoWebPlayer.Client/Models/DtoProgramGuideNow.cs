namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// The "now/next" answer of the program guide ("Programmauskunft") for one point in time:
    /// which broadcast runs right now (with its progress inside the video) and which one follows.
    /// The answer is purely time-based and stateless — it depends only on the clock and the
    /// schedule, never on viewers or process state.
    /// </summary>
    public class DtoProgramGuideNow
    {
        /// <summary>
        /// Gets or sets the UTC time the answer was computed for (the server's clock).
        /// </summary>
        public DateTimeOffset ServerTimeUtc { get; set; }

        /// <summary>
        /// Gets or sets the currently running broadcast, or <see langword="null"/> when the
        /// schedule has a gap at <see cref="ServerTimeUtc"/> (or no channel exists yet).
        /// </summary>
        public DtoProgramGuideEntry? Now { get; set; }

        /// <summary>
        /// Gets or sets the next scheduled broadcast after the current one, or
        /// <see langword="null"/> when nothing is scheduled ahead.
        /// </summary>
        public DtoProgramGuideEntry? Next { get; set; }
    }
}
