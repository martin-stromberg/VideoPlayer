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

        // Normalize request token (accept optional Bearer prefix)
        string requestToken = string.Empty;
        if (tokenHeader.Count > 0)
            requestToken = tokenHeader.ToString().Trim();
        if (requestToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            requestToken = requestToken.Substring(7).Trim();

        // Prüfe mehrere konfigurierte Tokens (jede Konfig-Einstellung kann mehrere, kommaseparierte Tokens enthalten)
        var validTokens = new HashSet<string>(StringComparer.Ordinal);

        void AddTokensFromConfig(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (!string.IsNullOrEmpty(t))
                    validTokens.Add(t);
            }
        }

        // Allgemeiner Token
        AddTokensFromConfig(config["Jwt:ApiToken"]);
        // Web-spezifischer Token
        AddTokensFromConfig(config["Jwt:ApiToken:Web"]);
        // MAUI-spezifischer Token
        AddTokensFromConfig(config["Jwt:ApiToken:Maui"]);

        // Prüfe, ob der Request-Token einem der gültigen Tokens entspricht
        if (validTokens.Contains(requestToken))
        {
            base.OnActionExecuting(context);
            return;
        }

        logger?.LogWarning("Ungültiger API-Token: {Token}", requestToken);
        context.Result = new UnauthorizedResult();
    }
}