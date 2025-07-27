using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Asn1.Ocsp;
using System.IO;
using System.Net;

namespace WebPlayerApi.Services
{
    public class IPCheck
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IPCheck> logger;

        public IPCheck(RequestDelegate next, ILogger<IPCheck> logger)
        {
            _next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            logger.LogInformation("Request: {Method} {Path} from {IP}", context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress?.ToString());

            var allowedAddresses = Dns.GetHostAddresses(context.Request.Host.Host).Select(i => i.ToString()).ToArray();
            var currentIP = context.Request.HttpContext.Connection.RemoteIpAddress.ToString();
            if (!allowedAddresses.Contains(currentIP))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await _next(context); // weiter zur nächsten Middleware
            logger.LogInformation("Response: {StatusCode} for {Path}", context.Response.StatusCode, context.Request.Path);
        }
    }
}
