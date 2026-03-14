using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Net;
using WebPlayerApi.Common.Models;
using WebPlayerApi.Services;

namespace WebPlayerApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InfoController : Controller
    {
        private readonly InMemoryLoggerProvider logger;

        public InfoController(InMemoryLoggerProvider logger)
        {
            this.logger = logger;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return Json(new InfoDto()
            {
                Host = this.Request.Host.ToString(),
                HostAddresses = Dns.GetHostAddresses(Request.Host.Host).Select(i => i.ToString()).ToArray(),
                RemoteIpAddress = this.Request.HttpContext.Connection.RemoteIpAddress.ToString()
            });
        }

        [HttpGet("logs")]
        public IActionResult Logs()
        {
            return Ok(logger.Logs);
        }
    }
}
