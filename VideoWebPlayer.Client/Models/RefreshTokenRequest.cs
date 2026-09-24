namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request payload for refreshing or revoking a session via refresh token.
    /// Serialized with camelCase JSON field name: refreshToken.
    /// </summary>
    public class RefreshTokenRequest
    {
        /// <summary>
        /// Gets or sets the plaintext refresh token.
        /// </summary>
        public string RefreshToken { get; set; } = "";
    }
}
