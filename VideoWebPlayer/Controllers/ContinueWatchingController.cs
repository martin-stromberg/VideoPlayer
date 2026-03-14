using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers
{
    /// <summary>
    /// Provides endpoints for continue-watching entries.
    /// </summary>
    [ApiController]
    [Route("api/continue-watching")]
    [BearerTokenCheck]
    public class ContinueWatchingController : ApiBaseController
    {
        private readonly ContinueWatchingService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContinueWatchingController"/> class.
        /// </summary>
        /// <param name="service">Continue-watching service.</param>
        /// <param name="authService">Authentication service.</param>
        /// <param name="logger">Logger instance.</param>
        public ContinueWatchingController(ContinueWatchingService service, IAuthService authService, ILogger<ContinueWatchingController> logger)
            :base(authService, logger)
        {
            _service = service;
        }

        /// <summary>
        /// Gets the current user's continue-watching list.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ContinueWatchingDto>>> GetAsync(CancellationToken ct)
        {
            var list = await _service.GetListAsync(User, ct);
            return Ok(list);
        }

        /// <summary>
        /// Request payload for reporting progress.
        /// </summary>
        public record ProgressRequest(
            /// <summary>Media type (movie or episode).</summary>
            string MediaType,
            /// <summary>Media identifier.</summary>
            long MediaId,
            /// <summary>Playback position in seconds.</summary>
            long PositionSeconds,
            /// <summary>Total duration in seconds.</summary>
            long DurationSeconds);

        /// <summary>
        /// Reports playback progress for the current user.
        /// </summary>
        /// <param name="req">The progress request.</param>
        /// <param name="ct">Cancellation token.</param>
        [HttpPost("progress")]
        public async Task<IActionResult> ReportProgress([FromBody] ProgressRequest req, CancellationToken ct)
        {
            CheckLogedIn();
            if (string.IsNullOrWhiteSpace(req.MediaType))
                return BadRequest("MediaType fehlt.");

            var mediaType = req.MediaType.Trim().ToLowerInvariant();
            var movieId = mediaType == "movie" ? req.MediaId : (long?)null;
            var episodeId = mediaType == "episode" || mediaType == nameof(TVShowEpisode).ToLower() ? req.MediaId : (long?)null;
            if (movieId is null && episodeId is null)
                return BadRequest("Unbekannter MediaType. Erwartet: 'movie' oder 'episode'.");
            await _service.ReportProgressAsync(CurrentUser, movieId, episodeId, TimeSpan.FromSeconds(req.PositionSeconds), TimeSpan.FromSeconds(req.DurationSeconds), ct);
            return NoContent();
        }
    }
}