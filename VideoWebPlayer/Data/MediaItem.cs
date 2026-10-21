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

        /// <summary>
        /// Gets or sets the probed playback duration of the media file, or <c>null</c> when no
        /// duration could be determined (or the file was never probed).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last duration probe, or <c>null</c> when the file was
        /// never probed. A set timestamp together with a <c>null</c> <see cref="Duration"/> marks a
        /// failed probe that must not be retried.
        /// </summary>
        public DateTime? DurationProbedAt { get; set; }
    }
}
