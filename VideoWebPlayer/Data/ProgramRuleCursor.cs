using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The persisted continuation state ("Fortsetzungsstand") of a <see cref="ProgramRule"/>:
    /// which element of the rule's expanded material list was scheduled last, so series and
    /// rotations continue in air order across generation runs and restarts. The foreign key to
    /// <see cref="ProgramRule"/> is a real cascade — deleting a rule discards its progress.
    /// </summary>
    public class ProgramRuleCursor
    {
        /// <summary>
        /// Gets or sets the cursor identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning rule identifier.
        /// </summary>
        [ForeignKey(nameof(ProgramRule))]
        public long ProgramRuleId { get; set; }

        /// <summary>
        /// Gets or sets the owning rule.
        /// </summary>
        public ProgramRule ProgramRule { get; set; } = null!;

        /// <summary>
        /// Gets or sets the kind of content <see cref="LastContentId"/> refers to.
        /// </summary>
        public ProgramCursorContentType LastContentType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the content element scheduled last within the rule's
        /// expanded material list (movie id, episode id or playlist entry id, depending on
        /// <see cref="LastContentType"/>).
        /// </summary>
        public long LastContentId { get; set; }

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
