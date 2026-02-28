using Microsoft.AspNetCore.Identity;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents an application user with additional metadata.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Gets or sets the serialized list of accessible sources.
        /// </summary>
        public string Sources { get; set; } = "";
        /// <summary>
        /// Gets or sets a value indicating whether the user is an administrator.
        /// </summary>
        public bool IsAdmin { get; set; } = false;
    }

}
