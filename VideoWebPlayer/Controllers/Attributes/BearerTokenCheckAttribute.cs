using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

public class BearerTokenCheckAttribute : ActionFilterAttribute
{

    public BearerTokenCheckAttribute()
    {
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var _jwtKey = context.HttpContext.RequestServices.GetService<SymmetricSecurityKey>();
        var _issuer = context.HttpContext.RequestServices.GetService<IConfiguration>()["Jwt:Issuer"];
        var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            authHeader = context.HttpContext.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(authHeader))
                authHeader = $"Bearer {authHeader}";
        }
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _issuer,
                IssuerSigningKey = _jwtKey
            }, out var validatedToken);

            context.HttpContext.User = principal;
        }
        catch
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        base.OnActionExecuting(context);
    }
}