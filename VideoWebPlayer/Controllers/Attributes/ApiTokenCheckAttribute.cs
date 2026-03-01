using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
/// <summary>
/// Prüft, ob der API-Key im Request angegeben ist.
/// Akzeptiert mehrere konfigurierte API-Tokens (für verschiedene Clients).
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
    /// Akzeptiert: Jwt:ApiToken (allgemein), Jwt:ApiToken:Web, Jwt:ApiToken:Maui
    /// </summary>
    /// <param name="context">The action executing context.</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var logger = context.HttpContext.RequestServices.GetService(typeof(ILogger<ApiTokenCheckAttribute>)) as ILogger<ApiTokenCheckAttribute>;
        var config = context.HttpContext.RequestServices.GetService<IConfiguration>();

        if (config == null)
        {
            logger?.LogError("Configuration service not available.");
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var tokenHeader) || tokenHeader.Count == 0)
        {
            logger?.LogWarning("API-Token fehlt im Header.");
            context.Result = new UnauthorizedResult();
            return;
        }

        var requestToken = tokenHeader.ToString();

        // Prüfe mehrere konfigurierte Tokens
        var validTokens = new List<string>();
        
        // Allgemeiner Token
        var apiToken = config["Jwt:ApiToken"];
        if (!string.IsNullOrWhiteSpace(apiToken))
            validTokens.Add(apiToken);
        
        // Web-spezifischer Token
        var webToken = config["Jwt:ApiToken:Web"];
        if (!string.IsNullOrWhiteSpace(webToken))
            validTokens.Add(webToken);
        
        // MAUI-spezifischer Token
        var mauiToken = config["Jwt:ApiToken:Maui"];
        if (!string.IsNullOrWhiteSpace(mauiToken))
            validTokens.Add(mauiToken);

        // Prüfe, ob der Request-Token einem der gültigen Tokens entspricht
        if (validTokens.Any(token => string.Equals(requestToken, token, StringComparison.Ordinal)))
        {
            base.OnActionExecuting(context);
            return;
        }

        logger?.LogWarning("Ungültiger API-Token: {Token}", requestToken);
        context.Result = new UnauthorizedResult();
    }
}