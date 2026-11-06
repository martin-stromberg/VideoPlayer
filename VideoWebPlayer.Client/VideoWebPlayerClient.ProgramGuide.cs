using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client
{
    // Implements IProgramGuideApiClient. Physically separated from VideoWebPlayerClient.cs (same
    // convention as VideoWebPlayerClient.Playlists.cs) while sharing its HttpGetAsync helper,
    // reauthorization handling and impersonation behavior.
    public partial class VideoWebPlayerClient
    {
        /// <inheritdoc />
        public async Task<DtoProgramGuideNow> RequestProgramGuideNowAsync(CancellationToken cancellationToken = default)
        {
            return await HttpGetAsync<DtoProgramGuideNow>("api/program-guide/now", cancellationToken);
        }

        /// <inheritdoc />
        public async Task<DtoProgramGuideEntry[]> RequestProgramGuideUpcomingAsync(int hours, CancellationToken cancellationToken = default)
        {
            return await HttpGetAsync<DtoProgramGuideEntry[]>($"api/program-guide/upcoming?hours={hours}", cancellationToken);
        }
    }
}
