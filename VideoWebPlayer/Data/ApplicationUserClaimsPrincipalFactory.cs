using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Adds application-specific claims to the user principal.
    /// </summary>
    public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationUserClaimsPrincipalFactory"/> class.
        /// </summary>
        /// <param name="userManager">User manager instance.</param>
        /// <param name="optionsAccessor">Identity options accessor.</param>
        public ApplicationUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, optionsAccessor)
        {
        }

        /// <summary>
        /// Generates claims for the specified user.
        /// </summary>
        /// <param name="user">The user instance.</param>
        /// <returns>The generated claims identity.</returns>
        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (user.IsAdmin)
            {
                identity.AddClaim(new Claim("IsAdmin", "True"));
            }
            return identity;
        }
    }
}