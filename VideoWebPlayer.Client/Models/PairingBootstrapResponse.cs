namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response payload of the pairing bootstrap.
    /// Serialized with camelCase JSON field names: serverPublicKey, encryptedPayload.
    /// </summary>
    public class PairingBootstrapResponse
    {
        /// <summary>
        /// Gets or sets the server ECDH public key (nistP256) as Base64-encoded SubjectPublicKeyInfo.
        /// </summary>
        public string ServerPublicKey { get; set; } = "";
        /// <summary>
        /// Gets or sets the AES-256-GCM encrypted payload as Base64(nonce | ciphertext | tag).
        /// The decrypted plaintext is a JSON object shaped like <see cref="PairingBootstrapPayload"/>.
        /// </summary>
        public string EncryptedPayload { get; set; } = "";
    }
}
