using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VideoWebPlayer.Services;

public class WhitelistIpMiddleware
{
    private readonly RequestDelegate _next;

    public WhitelistIpMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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