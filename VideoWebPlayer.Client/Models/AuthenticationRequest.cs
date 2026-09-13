namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for authenticating a user with email and password.
    /// </summary>
    public class AuthenticationRequest
    {
        /// <summary>
        /// Gets or sets the email address of the user.
        /// </summary>
        public string Email { get; set; } = "";

        /// <summary>
        /// Gets or sets the password of the user.
        /// </summary>
        public string Password { get; set; } = "";
    }
}
