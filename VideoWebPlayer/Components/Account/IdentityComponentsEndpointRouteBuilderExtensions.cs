using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;
using VideoWebPlayer.Components.Account.Pages;
using VideoWebPlayer.Components.Account.Pages.Manage;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            }).DisableAntiforgery();

            // Login endpoint - no antiforgery required
            accountGroup.MapPost("/LoginProcess", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromServices] ILoginIpBlockService loginIpBlockService,
                [FromForm] string email,
                [FromForm] string password,
                [FromForm] string? rememberMe,
                [FromForm] string? returnUrl) =>
            {
                var remoteIp = context.Connection.RemoteIpAddress;

                if (remoteIp != null && loginIpBlockService.IsBlocked(remoteIp))
                {
                    var errorQuery = QueryString.Create("error", "Invalid login attempt.");
                    return TypedResults.LocalRedirect($"~/Account/Login{errorQuery}");
                }

                var rememberMeFlag = !string.IsNullOrEmpty(rememberMe);
                var result = await signInManager.PasswordSignInAsync(email, password, rememberMeFlag, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return TypedResults.LocalRedirect($"~/{returnUrl ?? ""}");
                }
                else if (result.RequiresTwoFactor)
                {
                    var query = QueryString.Create(new Dictionary<string, string>
                    {
                        ["returnUrl"] = returnUrl ?? "",
                        ["rememberMe"] = rememberMeFlag.ToString()
                    });
                    return TypedResults.LocalRedirect($"~/Account/LoginWith2fa{query}");
                }
                else if (result.IsLockedOut)
                {
                    return TypedResults.LocalRedirect("~/Account/Lockout");
                }
                else
                {
                    if (remoteIp != null)
                        loginIpBlockService.RegisterFailure(remoteIp);

                    var errorQuery = QueryString.Create("error", "Invalid login attempt.");
                    return TypedResults.LocalRedirect($"~/Account/Login{errorQuery}");
                }
            }).DisableAntiforgery();

            // Logout - no antiforgery required
            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                SignInManager<ApplicationUser> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            }).DisableAntiforgery();

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<ApplicationUser> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            }).DisableAntiforgery();

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<ApplicationUser> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            }).DisableAntiforgery();

            return accountGroup;
        }
    }
}
