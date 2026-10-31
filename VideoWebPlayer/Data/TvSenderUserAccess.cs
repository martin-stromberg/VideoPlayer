using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Per-user release of the automated TV channel ("TV-Sender"): one row per user allowed
    /// to see the navigation entry, open <c>/live-tv</c> and call the
    /// <c>/api/program-guide</c>/<c>/api/live-stream</c> endpoints. Only effective together
    /// with the global switch <see cref="Setup.TvSenderEnabled"/>; administrators always have
    /// implicit access and need no row here.
    /// </summary>
    public class TvSenderUserAccess
    {
        /// <summary>
        /// Gets or sets the user identifier (FK to <c>AspNetUsers</c>).
        /// </summary>
        [Key]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user.
        /// </summary>
        public ApplicationUser User { get; set; } = null!;
    }
}
