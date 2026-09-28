namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request payload for redeeming a bootstrap ticket against device token, user session and refresh token.
    /// Serialized with camelCase JSON field names: ticket, clientPublicKey, deviceName.
    /// </summary>
    public class PairingBootstrapRequest
    {
        /// <summary>
        /// Gets or sets the bootstrap ticket (long secret from the QR code) or its 8-character short code alias.
        /// </summary>
        public string Ticket { get; set; } = "";
        /// <summary>
        /// Gets or sets the client ECDH public key (nistP256) as Base64-encoded SubjectPublicKeyInfo.
        /// </summary>
        public string ClientPublicKey { get; set; } = "";
        /// <summary>
        /// Gets or sets the optional display name of the device (max. 200 characters).
        /// </summary>
        public string? DeviceName { get; set; }
    }
}
