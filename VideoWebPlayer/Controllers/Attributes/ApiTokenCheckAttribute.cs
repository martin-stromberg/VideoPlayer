using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Checks whether a configured API key is present on the request.
/// </summary>
public class ApiTokenCheckAttribute : ActionFilterAttribute
{
    private const string HeaderName = "X-API-Key";
    private readonly ApiTokenScope _scope;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiTokenCheckAttribute"/> class.
    /// </summary>
    public ApiTokenCheckAttribute(ApiTokenScope scope = ApiTokenScope.AnyClient)
    {
        _scope = scope;
    }

    /// <summary>
    /// Validates the API token header before the action executes.
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
            logger?.LogWarning("API token header is missing.");
            context.Result = new UnauthorizedResult();
            return;
        }

        var requestToken = tokenHeader.ToString().Trim();
        if (requestToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            requestToken = requestToken.Substring(7).Trim();

        // Each configuration value may contain multiple comma- or semicolon-separated tokens.
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

        if (_scope == ApiTokenScope.MauiOnly)
        {
            AddTokensFromConfig(config["Jwt:ApiToken:Maui"]);
        }
        else
        {
            AddTokensFromConfig(config["Jwt:ApiToken"]);
            AddTokensFromConfig(config["Jwt:ApiToken:Web"]);
            AddTokensFromConfig(config["Jwt:ApiToken:Maui"]);
        }

        if (validTokens.Contains(requestToken))
        {
            base.OnActionExecuting(context);
            return;
        }

        logger?.LogWarning("Invalid API token for scope {Scope}.", _scope);
        context.Result = new UnauthorizedResult();
    }
}

/// <summary>
/// Determines which configured API tokens are accepted by <see cref="ApiTokenCheckAttribute"/>.
/// </summary>
public enum ApiTokenScope
{
    /// <summary>
    /// Accepts the legacy, web-specific, and MAUI-specific API tokens.
    /// </summary>
    AnyClient,

    /// <summary>
    /// Accepts only tokens configured via <c>Jwt:ApiToken:Maui</c>.
    /// </summary>
    MauiOnly
}
