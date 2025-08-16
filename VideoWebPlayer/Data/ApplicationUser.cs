using Microsoft.AspNetCore.Identity;

namespace VideoWebPlayer.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string Sources { get; set; } = "";
        public bool IsAdmin { get; set; } = false;
    }

}
