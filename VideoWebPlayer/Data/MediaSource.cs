using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert eine Medienquelle (z.B. SFTP-Server).
    /// </summary>
    public class MediaSource : MediaEntry
    {
        /// <summary>
        /// Gets or sets the host name of the source.
        /// </summary>
        public string Host { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the port of the source.
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// Gets or sets the username for authentication.
        /// </summary>
        public string? Username { get; set; }
        /// <summary>
        /// Gets or sets the password for authentication.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Optional uploaded icon image (stored in `MediaSourceIcons` table).
        /// </summary>
        public long? IconPictureId { get; set; }

        /// <summary>
        /// Navigation property for the uploaded source icon.
        /// </summary>
        public MediaSourceIcon? IconPicture { get; set; }
        /// <summary>
        /// Gets or sets the last scan timestamp.
        /// </summary>
        public DateTime? LastScannedAt { get; set; }
        /// <summary>
        /// Gets the media collections for this source.
        /// </summary>
        public ICollection<MediaCollection> MediaCollections { get; set; } = new List<MediaCollection>();
        /// <summary>
        /// Gets the users that have access to this source.
        /// </summary>
        public ICollection<MediaSourceUser> MediaSourceUsers { get; set; } = new List<MediaSourceUser>();
    }
}