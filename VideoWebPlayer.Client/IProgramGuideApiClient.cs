using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    /// <summary>
    /// Client-side operations for the program guide ("Programmauskunft") API (<c>api/program-guide</c>).
    /// Narrower than the full <see cref="VideoWebPlayerClient"/> facade, so Razor components that only
    /// show the live TV guide (<c>LiveTv</c>) can depend on just this surface — same pattern as
    /// <see cref="IPlaylistApiClient"/> for playlists. Implemented by <see cref="VideoWebPlayerClient"/>
    /// itself (see the <c>VideoWebPlayerClient.ProgramGuide.cs</c> partial file), so it shares the same
    /// HTTP client, reauthorization and impersonation behavior as the rest of the facade.
    /// </summary>
    public interface IProgramGuideApiClient
    {
        /// <summary>
        /// Requests which broadcast runs right now (with its position inside the video and its
        /// remaining duration) and which one follows — purely time-based, independent of viewers.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The "now/next" answer.</returns>
        Task<DtoProgramGuideNow> RequestProgramGuideNowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the bounded program overview of the coming hours: the running broadcast plus the
        /// scheduled broadcasts starting within the preview window.
        /// </summary>
        /// <param name="hours">How many hours ahead to look (1–12; the server enforces the cap).</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The program entries in start order.</returns>
        Task<DtoProgramGuideEntry[]> RequestProgramGuideUpcomingAsync(int hours, CancellationToken cancellationToken = default);
    }
}
