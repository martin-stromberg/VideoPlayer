using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity: one <see cref="ProgramClipGroup"/> assigned to a <see cref="ProgramRule"/>
    /// (clip groups are an additional assignment independent of the rule's material type).
    /// Both foreign keys cascade: deleting the rule or the group removes the link.
    /// </summary>
    public class ProgramRuleClipGroup
    {
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
        /// Gets or sets the clip group identifier.
        /// </summary>
        [ForeignKey(nameof(ClipGroup))]
        public long ProgramClipGroupId { get; set; }

        /// <summary>
        /// Gets or sets the clip group.
        /// </summary>
        public ProgramClipGroup ClipGroup { get; set; } = null!;
    }
}
