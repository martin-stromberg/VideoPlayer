using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// An authorization token issued by the server, together with its expiration time.
    /// </summary>
    public class AuthorizationToken
    {
        /// <summary>
        /// Gets or sets the raw token value.
        /// </summary>
        public string token { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the point in time at which the token expires.
        /// </summary>
        public DateTime expires { get; set; }
    }
}
