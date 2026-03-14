using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VideoWebPlayer.Services;

/// <summary>
/// Middleware that records IPs of authenticated users.
/// </summary>
public class WhitelistIpMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhitelistIpMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware delegate.</param>
    public WhitelistIpMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invokes the middleware with the current HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var internalConnectionManager = context.RequestServices.GetRequiredService<InternalConnectionService>();
            var remoteIp = context.Connection.RemoteIpAddress;
            internalConnectionManager.Allow(remoteIp);
        }
        await _next(context);
    }
}