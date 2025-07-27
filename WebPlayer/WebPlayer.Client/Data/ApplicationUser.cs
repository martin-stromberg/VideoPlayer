using Microsoft.AspNetCore.Identity;

namespace WebPlayer.Data
{

    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser, IApplicationUser
    {
        public string Sources { get; set; }
    }

    public class AppUser: IApplicationUser
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Sources { get; set; }
        public AppUser() { }
        public AppUser(ApplicationUser user)
        {
            Id = user.Id;
            UserName = user.UserName;
            Sources = user.Sources;
        }
    }

}
