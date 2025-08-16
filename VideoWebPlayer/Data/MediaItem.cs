using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Repräsentiert eine Mediendatei.
    /// </summary>
    public class MediaItem : MediaEntry
    {
        public long MediaCollectionId { get; set; }                
        public MediaCollection MediaCollection { get; set; } = null!;        
    }
}