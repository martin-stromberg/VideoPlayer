namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a short-lived one-time pairing code used to pair a device.
    /// </summary>
    public class PairingCode
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the pairing direction this code belongs to.
        /// </summary>
        public PairingCodeKind Kind { get; set; } = PairingCodeKind.AdminCode;
        /// <summary>
        /// Gets or sets the SHA-256 hash of the pairing code (for bootstrap tickets: hash of the short code alias).
        /// </summary>
        public string CodeHash { get; set; } = "";
        /// <summary>
        /// Gets or sets the SHA-256 hash of the long bootstrap ticket secret; <c>null</c> for admin codes.
        /// </summary>
        public string? TicketHash { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the pairing code was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the pairing code expires.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the pairing code was consumed.
        /// </summary>
        public DateTime? ConsumedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the id of the administrator who created the pairing code.
        /// </summary>
        public string? CreatedByUserId { get; set; }
    }
}
