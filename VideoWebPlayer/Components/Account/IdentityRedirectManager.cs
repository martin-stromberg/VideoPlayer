using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace VideoWebPlayer.Components.Account
{
    internal sealed class IdentityRedirectManager(
        NavigationManager navigationManager,
        IHttpContextAccessor httpContextAccessor)
    {
        public const string StatusCookieName = "Identity.StatusMessage";

        private static readonly CookieBuilder StatusCookieBuilder = new()
        {
            SameSite = SameSiteMode.Strict,
            HttpOnly = true,
            IsEssential = true,
            MaxAge = TimeSpan.FromSeconds(5),
        };

        public void RedirectTo(string? uri)
        {
            RedirectResponse(uri, httpContextAccessor.HttpContext);
        }

        public void RedirectTo(string uri, Dictionary<string, object?> queryParameters)
        {
            var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
            var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
            RedirectTo(newUri);
        }

        public void RedirectToWithStatus(string uri, string message, HttpContext context)
        {
            context.Response.Cookies.Append(StatusCookieName, message, StatusCookieBuilder.Build(context));
            RedirectResponse(uri, context);
        }

        private string CurrentPath => navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path);

        public void RedirectToCurrentPage() => RedirectTo(CurrentPath);

        public void RedirectToCurrentPageWithStatus(string message, HttpContext context)
            => RedirectToWithStatus(CurrentPath, message, context);

        private void RedirectResponse(string? uri, HttpContext? context)
        {
            uri ??= "";

            // Prevent open redirects.
            if (!Uri.IsWellFormedUriString(uri, UriKind.Relative))
            {
                uri = navigationManager.ToBaseRelativePath(uri);
            }

            uri = navigationManager
                .ToAbsoluteUri(uri)
                .GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);

            context ??= httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    $"{nameof(IdentityRedirectManager)} benötigt einen aktiven HTTP-Kontext.");

            if (context.Response.HasStarted)
            {
                throw new InvalidOperationException(
                    $"{nameof(IdentityRedirectManager)} kann nicht weiterleiten, nachdem die HTTP-Antwort begonnen hat.");
            }

            context.Response.Redirect(uri);
        }
    }
}
