using Microsoft.AspNetCore.Identity;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Components.Account
{
    internal sealed class IdentityUserAccessor(UserManager<ApplicationUser> userManager, IdentityRedirectManager redirectManager)
    {
        public async Task<ApplicationUser> GetRequiredUserAsync(HttpContext context)
        {
            var user = await userManager.GetUserAsync(context.User);

            if (user is null)
            {
                redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with ID '{userManager.GetUserId(context.User)}'.", context);
                throw new InvalidOperationException("Die Weiterleitung für den fehlenden Benutzer wurde unerwartet beendet.");
            }

            return user;
        }
    }
}
