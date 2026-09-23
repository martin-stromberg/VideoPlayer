namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a device that has been paired with the server via a pairing code.
    /// </summary>
    public class PairedDevice
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the display name of the paired device.
        /// </summary>
        public string Name { get; set; } = "";
        /// <summary>
        /// Gets or sets the SHA-256 hash of the device token.
        /// </summary>
        public string TokenHash { get; set; } = "";
        /// <summary>
        /// Gets or sets the timestamp when the device token was issued.
        /// </summary>
        public DateTime IssuedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the device token was last used.
        /// </summary>
        public DateTime? LastUsedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the device token was revoked.
        /// </summary>
        public DateTime? RevokedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the id of the administrator who created the pairing code.
        /// </summary>
        public string? CreatedByUserId { get; set; }
    }
}
