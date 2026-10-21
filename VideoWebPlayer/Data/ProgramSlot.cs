using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a single scheduled broadcast slot ("Sendeplatz") of a <see cref="TVChannel"/>:
    /// one concrete movie or TV show episode occupying <see cref="Start"/> to <see cref="End"/> (UTC)
    /// in the rolling program schedule. The references to the media file, the content and the
    /// producing rule are deliberately soft references without database foreign keys — the schedule
    /// must survive deletion of media files and rules so that the plan history stays traceable
    /// (mirroring <see cref="ProgramRule.MaterialId"/>).
    /// </summary>
    public class ProgramSlot
    {
        /// <summary>
        /// Gets or sets the slot identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning channel identifier.
        /// </summary>
        [ForeignKey(nameof(TVChannel))]
        public long TVChannelId { get; set; }

        /// <summary>
        /// Gets or sets the owning channel.
        /// </summary>
        public TVChannel TVChannel { get; set; } = null!;

        /// <summary>
        /// Gets or sets the UTC start time of the broadcast.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Gets or sets the UTC end time of the broadcast (<see cref="Start"/> plus the media
        /// item's duration).
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Gets or sets the referenced media item identifier — deliberately no database foreign
        /// key, so a deleted media item does not cascade-delete the schedule history.
        /// </summary>
        public long MediaItemId { get; set; }

        /// <summary>
        /// Gets or sets the movie identifier when the slot airs a movie, otherwise
        /// <see langword="null"/> (soft reference, no foreign key).
        /// </summary>
        public long? MovieId { get; set; }

        /// <summary>
        /// Gets or sets the episode identifier when the slot airs a TV show episode, otherwise
        /// <see langword="null"/> (soft reference, no foreign key).
        /// </summary>
        public long? TVShowEpisodeId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the <see cref="ProgramRule"/> that produced this slot,
        /// or <see langword="null"/> when the rule no longer exists (soft reference, no foreign
        /// key — deleting a rule must not erase the schedule history).
        /// </summary>
        public long? ProgramRuleId { get; set; }

        /// <summary>
        /// Gets or sets a denormalized snapshot of the producing rule's
        /// <see cref="ProgramRule.RuleType"/> — identifies filler slots even after the rule was
        /// deleted.
        /// </summary>
        public ProgramRuleType RuleType { get; set; }

        /// <summary>
        /// Gets or sets the lifecycle status of the slot: <see cref="ProgramSlotStatus.Scheduled"/>
        /// slots occupy air time; <see cref="ProgramSlotStatus.Displaced"/> slots were invalidated
        /// by a schedule re-evaluation and are kept only for traceability.
        /// </summary>
        public ProgramSlotStatus Status { get; set; } = ProgramSlotStatus.Scheduled;

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
