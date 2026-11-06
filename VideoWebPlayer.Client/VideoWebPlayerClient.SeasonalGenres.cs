using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Client;

public partial class VideoWebPlayerClient
{
    /// <summary>Loads the authenticated user's active, nonempty seasonal home sections.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The ordered sections, including their server-defined expiry.</returns>
    public virtual Task<List<DtoSeasonalGenre>> GetSeasonalGenresAsync(CancellationToken cancellationToken = default)
        => HttpGetAsync<List<DtoSeasonalGenre>>("api/home/seasonal-genres", cancellationToken);
}
