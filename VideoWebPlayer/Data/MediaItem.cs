using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert eine Mediendatei.
    /// </summary>
    public class MediaItem : MediaEntry
    {
        /// <summary>
        /// Gets or sets the owning media collection identifier.
        /// </summary>
        public long MediaCollectionId { get; set; }                
        /// <summary>
        /// Gets or sets the owning media collection.
        /// </summary>
        public MediaCollection MediaCollection { get; set; } = null!;        
    }
}