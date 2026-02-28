using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert ein Verzeichnis/Sammlung innerhalb einer Quelle.
    /// </summary>
    public class MediaCollection : MediaEntry
    {
        /// <summary>
        /// Gets or sets the owning media source identifier.
        /// </summary>
        public long MediaSourceId { get; set; }
        /// <summary>
        /// Gets or sets the parent collection identifier.
        /// </summary>
        public long? ParentMediaCollectionId { get; set; }
        /// <summary>
        /// Gets or sets the last scan timestamp.
        /// </summary>
        public DateTime? LastScannedAt { get; set; }
        /// <summary>
        /// Gets or sets the next scheduled scan time.
        /// </summary>
        public DateTime? ScanDueAt{ get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this collection is fully scanned and ready to classify.
        /// </summary>
        public bool Classifyable { get; set; }
        /// <summary>
        /// Gets or sets the owning media source.
        /// </summary>
        public MediaSource MediaSource { get; set; } = null!;
        /// <summary>
        /// Gets or sets the parent media collection.
        /// </summary>
        public MediaCollection? ParentMediaCollection { get; set; }
        /// <summary>
        /// Gets the child media collections.
        /// </summary>
        public ICollection<MediaCollection> ChildCollections { get; set; } = new List<MediaCollection>();
        /// <summary>
        /// Gets the media items contained in this collection.
        /// </summary>
        public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
        /// <summary>
        /// Gets a value indicating whether this collection should be skipped in scans.
        /// </summary>
        public bool Skip { get; internal set; }
    }
}