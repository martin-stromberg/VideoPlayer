namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response payload of a refresh-token rotation.
    /// Serialized with camelCase JSON field names: token, expires, refreshToken.
    /// </summary>
    public class RefreshTokenResponse
    {
        /// <summary>
        /// Gets or sets the new JWT access token.
        /// </summary>
        public string Token { get; set; } = "";
        /// <summary>
        /// Gets or sets the expiry timestamp of the JWT access token (UTC).
        /// </summary>
        public DateTime Expires { get; set; }
        /// <summary>
        /// Gets or sets the new refresh token (rotation — the presented token is revoked).
        /// </summary>
        public string RefreshToken { get; set; } = "";
    }
}
