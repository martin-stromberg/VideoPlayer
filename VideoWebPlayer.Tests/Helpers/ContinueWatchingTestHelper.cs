using System.Net.Http.Headers;
using System.Net.Http.Json;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared HTTP-level helpers for playlist-bound continue-watching reports, used by both the paired-device
/// tests (A6, <see cref="DeviceClientTestBase"/>) and the local-media-source tests (A7,
/// <see cref="LocalMediaPlaylistE2ETestBase"/>): reporting progress with a <c>playlistId</c> is a real HTTP
/// call in both, since <c>VideoWebPlayerClient.ReportPlaybackProgressAsync</c> has no <c>playlistId</c>
/// parameter.
/// </summary>
public static class ContinueWatchingTestHelper
{
    /// <summary>
    /// Reports playback progress for a title, optionally played from a playlist.
    /// </summary>
    /// <param name="http">The HTTP client to send the request on.</param>
    /// <param name="bearerToken">The bearer token to authenticate with.</param>
    /// <param name="movieId">The id of the movie the progress belongs to.</param>
    /// <param name="playlistId">The id of the playlist the title is played from, or <see langword="null"/>.</param>
    /// <param name="ct">The cancellation token of the running test.</param>
    /// <returns>The server's answer.</returns>
    public static Task<HttpResponseMessage> ReportProgressAsync(HttpClient http, string bearerToken, long movieId, long? playlistId, CancellationToken ct)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return http.PostAsJsonAsync(
            "/api/continue-watching/progress",
            new { mediaType = "movie", mediaId = movieId, positionSeconds = 120, durationSeconds = 3600, playlistId },
            ct);
    }

    /// <summary>
    /// Polls the continue-watching list until the expected entry appears (the server persists reported
    /// progress through <c>ContinueWatchingWorker</c>, a moment after answering the request).
    /// </summary>
    /// <param name="client">The playlist/media API client to poll with.</param>
    /// <param name="predicate">Identifies the awaited entry.</param>
    /// <param name="ct">The cancellation token of the running test.</param>
    /// <returns>The matching entry.</returns>
    public static async Task<ContinueWatchingDto> WaitForEntryAsync(VideoWebPlayerClient client, Func<ContinueWatchingDto, bool> predicate, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var match = (await client.RequestContinueWatchingAsync()).FirstOrDefault(predicate);
            if (match is not null)
                return match;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Der erwartete Weiterschauen-Eintrag ist nicht erschienen.");

            await Task.Delay(100, ct);
        }
    }
}
