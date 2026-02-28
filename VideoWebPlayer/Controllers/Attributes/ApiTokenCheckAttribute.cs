using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
/// <summary>
/// Prüft, ob der API-Key im Request angegeben ist.
/// </summary>
public class ApiTokenCheckAttribute : ActionFilterAttribute
{
    private const string HeaderName = "X-API-Key";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiTokenCheckAttribute"/> class.
    /// </summary>
    public ApiTokenCheckAttribute()
    {
    }

    /// <summary>
    /// Validates the API token header before the action executes.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<ApiTokenCheckAttribute>)) as ILogger<ApiTokenCheckAttribute>;
        var apiTokenKey = context.HttpContext.RequestServices.GetService<IConfiguration>()["Jwt:ApiToken"];

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var tokenHeader) || tokenHeader.Count == 0)
        {
            logger?.LogWarning("API-Token fehlt im Header.");
            context.Result = new UnauthorizedResult();
            return;
        }

        var requestToken = tokenHeader.ToString();
        if (!string.Equals(requestToken, apiTokenKey, StringComparison.Ordinal))
        {
            logger?.LogWarning("Ungültiger API-Token: {Token}", requestToken);
            context.Result = new UnauthorizedResult();
            return;
        }

        base.OnActionExecuting(context);
    }
}