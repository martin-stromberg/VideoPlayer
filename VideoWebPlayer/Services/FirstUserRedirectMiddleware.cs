using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Leitet HTML-Anfragen zur Registrierung weiter, solange noch kein Benutzer existiert.
/// </summary>
public sealed class FirstUserRedirectMiddleware
{
    private const string SystemUserNormalizedName = "SYSTEM";
    private readonly RequestDelegate _next;
    private int _hasApplicationUser;

    /// <summary>
    /// Erstellt die Middleware für die Weiterleitung des Erstbenutzers.
    /// </summary>
    /// <param name="next">Die nächste Middleware in der Pipeline.</param>
    public FirstUserRedirectMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    /// <summary>
    /// Prüft die Anfrage und führt bei Bedarf die Weiterleitung aus.
    /// </summary>
    /// <param name="context">Der aktuelle HTTP-Kontext.</param>
    /// <param name="userManager">Der Identity-Benutzermanager.</param>
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (Volatile.Read(ref _hasApplicationUser) == 0 && ShouldRedirect(context.Request))
        {
            var hasApplicationUser = await userManager.Users
                .AnyAsync(user => user.NormalizedUserName != SystemUserNormalizedName, context.RequestAborted);

            if (hasApplicationUser)
            {
                Interlocked.Exchange(ref _hasApplicationUser, 1);
            }
            else
            {
                context.Response.Redirect(BuildRegisterLocation(context.Request));
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldRedirect(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method)
            || !AcceptsHtml(request)
            || IsExcludedPath(request.Path)
            || Path.HasExtension(request.Path.Value))
        {
            return false;
        }

        return true;
    }

    private static bool AcceptsHtml(HttpRequest request)
    {
        return request.Headers.Accept.Any(value =>
            value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcludedPath(PathString path)
    {
        return path.StartsWithSegments("/Account/Register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/_content", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRegisterLocation(HttpRequest request)
    {
        var returnUrl = request.Query
            .Where(parameter => parameter.Key.Equals("ReturnUrl", StringComparison.OrdinalIgnoreCase))
            .Select(parameter => parameter.Value.FirstOrDefault() ?? string.Empty)
            .FirstOrDefault();

        returnUrl ??= $"{request.PathBase}{request.Path}{request.QueryString}";
        return QueryHelpers.AddQueryString("/Account/Register", "ReturnUrl", returnUrl);
    }
}
