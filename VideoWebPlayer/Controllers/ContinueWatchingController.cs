using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers
{
    [ApiController]
    [Route("api/continue-watching")]
    [BearerTokenCheck]
    public class ContinueWatchingController : ApiBaseController
    {
        private readonly ContinueWatchingService _service;

        public ContinueWatchingController(ContinueWatchingService service, IAuthService authService, ILogger<ContinueWatchingController> logger)
            :base(authService, logger)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<ContinueWatchingDto>>> GetAsync(CancellationToken ct)
        {
            var list = await _service.GetListAsync(User, ct);
            return Ok(list);
        }

        public record ProgressRequest(string MediaType, long MediaId, long PositionSeconds, long DurationSeconds);

        [HttpPost("progress")]
        public async Task<IActionResult> ReportProgress([FromBody] ProgressRequest req, CancellationToken ct)
        {
            CheckLogedIn();
            var movieId = req.MediaType == "movie" ? req.MediaId : (long?)null;
            var episodeId = req.MediaType == "episode" ? req.MediaId : (long?)null;
            await _service.ReportProgressAsync(CurrentUser, movieId, episodeId, TimeSpan.FromSeconds(req.PositionSeconds), TimeSpan.FromSeconds(req.DurationSeconds), ct);
            return NoContent();
        }
    }
}