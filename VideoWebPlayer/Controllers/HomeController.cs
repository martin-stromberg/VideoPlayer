using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>Exposes server-selected home sections to authenticated clients.</summary>
[ApiController]
[Route("api/home")]
[BearerTokenCheck]
public sealed class HomeController(SeasonalHomeService seasonalHome, IAuthService authService, ILogger<HomeController> logger)
    : ApiBaseController(authService, logger)
{
    /// <summary>Returns active seasonal genres with authorized media and server expiry.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Nonempty sections, or an empty array when no season is active.</returns>
    [HttpGet("seasonal-genres")]
    public async Task<ActionResult<List<DtoSeasonalGenre>>> GetSeasonalGenres(CancellationToken cancellationToken)
    {
        try
        {
            CheckLoggedIn();
            return Ok(await seasonalHome.GetAsync(CurrentUser!.Id, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
