namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Decrypted content of <see cref="PairingBootstrapResponse.EncryptedPayload"/>.
    /// Serialized with camelCase JSON field names: deviceToken, token, expires, refreshToken.
    /// </summary>
    public class PairingBootstrapPayload
    {
        /// <summary>
        /// Gets or sets the plaintext device token (gate key for API calls).
        /// </summary>
        public string DeviceToken { get; set; } = "";
        /// <summary>
        /// Gets or sets the JWT access token of the user session.
        /// </summary>
        public string Token { get; set; } = "";
        /// <summary>
        /// Gets or sets the expiry timestamp of the JWT access token (UTC).
        /// </summary>
        public DateTime Expires { get; set; }
        /// <summary>
        /// Gets or sets the refresh token for session renewal with rotation.
        /// </summary>
        public string RefreshToken { get; set; } = "";
    }
}
