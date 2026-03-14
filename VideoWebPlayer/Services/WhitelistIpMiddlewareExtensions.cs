using Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for registering whitelist IP middleware.
/// </summary>
public static class WhitelistIpMiddlewareExtensions
{
    /// <summary>
    /// Adds <see cref="WhitelistIpMiddleware"/> to the request pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseWhitelistIp(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<WhitelistIpMiddleware>();
    }
}