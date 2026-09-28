namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a long-lived refresh token bound to a user and a paired device.
    /// Only the SHA-256 hash of the token is persisted.
    /// </summary>
    public class RefreshToken
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Gets or sets the SHA-256 hash of the refresh token.
        /// </summary>
        public string TokenHash { get; set; } = "";
        /// <summary>
        /// Gets or sets the id of the user the token belongs to.
        /// </summary>
        public string UserId { get; set; } = "";
        /// <summary>
        /// Gets or sets the id of the paired device the token is bound to.
        /// </summary>
        public int DeviceId { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the token was created.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the token expires.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the token was revoked; <c>null</c> while active.
        /// </summary>
        public DateTime? RevokedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the SHA-256 hash of the token that replaced this token during rotation; <c>null</c> unless rotated.
        /// </summary>
        public string? ReplacedByHash { get; set; }
    }
}
