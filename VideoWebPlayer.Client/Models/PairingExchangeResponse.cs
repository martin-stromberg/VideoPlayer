namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response payload of the pairing exchange.
    /// Serialized with camelCase JSON field names: serverPublicKey, encryptedToken.
    /// </summary>
    public class PairingExchangeResponse
    {
        /// <summary>
        /// Gets or sets the server ECDH public key (nistP256) as Base64-encoded SubjectPublicKeyInfo.
        /// </summary>
        public string ServerPublicKey { get; set; } = "";
        /// <summary>
        /// Gets or sets the AES-256-GCM encrypted device token as Base64(nonce | ciphertext | tag).
        /// </summary>
        public string EncryptedToken { get; set; } = "";
    }
}
