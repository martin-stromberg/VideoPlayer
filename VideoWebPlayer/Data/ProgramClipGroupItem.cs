using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity: one clip (<see cref="MediaItem"/>) assigned to a <see cref="ProgramClipGroup"/>.
    /// A clip may belong to several groups. Both foreign keys cascade: deleting the group or the
    /// media item removes the membership.
    /// </summary>
    public class ProgramClipGroupItem
    {
        /// <summary>
        /// Gets or sets the owning group identifier.
        /// </summary>
        [ForeignKey(nameof(ProgramClipGroup))]
        public long ProgramClipGroupId { get; set; }

        /// <summary>
        /// Gets or sets the owning group.
        /// </summary>
        public ProgramClipGroup ProgramClipGroup { get; set; } = null!;

        /// <summary>
        /// Gets or sets the media item identifier.
        /// </summary>
        [ForeignKey(nameof(MediaItem))]
        public long MediaItemId { get; set; }

        /// <summary>
        /// Gets or sets the media item.
        /// </summary>
        public MediaItem MediaItem { get; set; } = null!;
    }
}
