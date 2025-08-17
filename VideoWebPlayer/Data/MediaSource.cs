using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert eine Medienquelle (z.B. SFTP-Server).
    /// </summary>
    public class MediaSource : MediaEntry
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public DateTime? LastScannedAt { get; set; }
        public ICollection<MediaCollection> MediaCollections { get; set; } = new List<MediaCollection>();
        public ICollection<MediaSourceUser> MediaSourceUsers { get; set; } = new List<MediaSourceUser>();
    }
}