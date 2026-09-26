using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Public endpoint for exchanging a one-time pairing code against a device token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PairingController : ApiBaseController
{
    private readonly IPairingService _pairingService;
    private readonly IPairingBootstrapService _bootstrapService;
    private readonly ILoginIpBlockService _ipBlockService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingController"/> class.
    /// </summary>
    /// <param name="authService">Authentication service.</param>
    /// <param name="pairingService">Pairing service.</param>
    /// <param name="bootstrapService">Bootstrap pairing service.</param>
    /// <param name="ipBlockService">IP block service used for brute-force protection.</param>
    /// <param name="logger">Logger instance.</param>
    public PairingController(
        IAuthService authService,
        IPairingService pairingService,
        IPairingBootstrapService bootstrapService,
        ILoginIpBlockService ipBlockService,
        ILogger<PairingController> logger)
        : base(authService, logger)
    {
        _pairingService = pairingService;
        _bootstrapService = bootstrapService;
        _ipBlockService = ipBlockService;
    }

    /// <summary>
    /// Exchanges a pairing code for an encrypted device token.
    /// </summary>
    /// <param name="request">The exchange request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange result.</returns>
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange([FromBody] PairingExchangeRequest request, CancellationToken cancellationToken)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (_ipBlockService.IsBlocked(remoteIp))
            return StatusCode(StatusCodes.Status429TooManyRequests);

        var result = await _pairingService.ExchangeAsync(request, cancellationToken);
        if (result.Success)
        {
            _ipBlockService.RegisterSuccess(remoteIp);
            return Ok(new PairingExchangeResponse
            {
                ServerPublicKey = result.ServerPublicKey!,
                EncryptedToken = result.EncryptedToken!
            });
        }

        if (result.Error == PairingExchangeErrorKind.InvalidRequest)
            return BadRequest("Ungültiger Pairing-Request.");

        _ipBlockService.RegisterFailure(remoteIp);
        return Unauthorized("Ungültiger oder abgelaufener Pairing-Code.");
    }

    /// <summary>
    /// Redeems a bootstrap ticket (or its short code alias) for an encrypted payload containing
    /// device token, user session JWT and refresh token.
    /// </summary>
    /// <param name="request">The bootstrap request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bootstrap result.</returns>
    [HttpPost("bootstrap")]
    public async Task<IActionResult> Bootstrap([FromBody] PairingBootstrapRequest request, CancellationToken cancellationToken)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress;
        if (_ipBlockService.IsBlocked(remoteIp))
            return StatusCode(StatusCodes.Status429TooManyRequests);

        var result = await _bootstrapService.BootstrapAsync(request, cancellationToken);
        if (result.Success)
        {
            _ipBlockService.RegisterSuccess(remoteIp);
            return Ok(new PairingBootstrapResponse
            {
                ServerPublicKey = result.ServerPublicKey!,
                EncryptedPayload = result.EncryptedPayload!
            });
        }

        if (result.Error == BootstrapTicketErrorKind.InvalidRequest)
            return BadRequest("Ungültiger Bootstrap-Request.");

        _ipBlockService.RegisterFailure(remoteIp);
        return Unauthorized("Ungültiges oder abgelaufenes Pairing-Ticket.");
    }
}
