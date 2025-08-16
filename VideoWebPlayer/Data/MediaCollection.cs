using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert ein Verzeichnis/Sammlung innerhalb einer Quelle.
    /// </summary>
    public class MediaCollection : MediaEntry
    {
        public long MediaSourceId { get; set; }
        public long? ParentMediaCollectionId { get; set; }
        public DateTime? LastScannedAt { get; set; }
        public MediaSource MediaSource { get; set; } = null!;
        public MediaCollection? ParentMediaCollection { get; set; }
        public ICollection<MediaCollection> ChildCollections { get; set; } = new List<MediaCollection>();
        public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
    }
}