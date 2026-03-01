using Microsoft.AspNetCore.Mvc;

namespace VideoWebPlayer.Controllers;

/// <summary>
/// Einfacher Health-Check-Endpunkt für Verbindungsprüfungen durch den Client.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Gibt "OK" zurück, wenn der Server läuft.
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok("OK");
}
