using Microsoft.AspNetCore.Builder;

public static class WhitelistIpMiddlewareExtensions
{
    public static IApplicationBuilder UseWhitelistIp(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<WhitelistIpMiddleware>();
    }
}