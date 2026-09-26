namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request payload for exchanging a pairing code against a device token.
    /// Serialized with camelCase JSON field names: code, clientPublicKey, deviceName.
    /// </summary>
    public class PairingExchangeRequest
    {
        /// <summary>
        /// Gets or sets the one-time pairing code.
        /// </summary>
        public string Code { get; set; } = "";
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
