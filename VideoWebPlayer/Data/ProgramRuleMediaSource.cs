using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity: one media source of the per-rule source filter ("Quell-Filter") used by
    /// <see cref="ProgramRuleMaterialType.Genre"/> program material of a <see cref="ProgramRule"/>.
    /// A rule without any <see cref="ProgramRuleMediaSource"/> rows considers all media sources.
    /// Both foreign keys cascade: deleting the rule or the media source removes the link.
    /// </summary>
    public class ProgramRuleMediaSource
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
        /// Gets or sets the media source identifier.
        /// </summary>
        [ForeignKey(nameof(MediaSource))]
        public long MediaSourceId { get; set; }

        /// <summary>
        /// Gets or sets the media source.
        /// </summary>
        public MediaSource MediaSource { get; set; } = null!;
    }
}
