namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for starting to impersonate another user.
    /// </summary>
    public class ImpersonateRequest
    {
        /// <summary>
        /// Gets or sets the identifier of the user to impersonate.
        /// </summary>
        public string UserId { get; set; } = "";
    }
}
