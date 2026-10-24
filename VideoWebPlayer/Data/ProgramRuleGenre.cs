using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity: one genre selected as program material of a <see cref="ProgramRule"/>
    /// (material type <see cref="ProgramRuleMaterialType.Genre"/>). The stored row is a concrete
    /// genre record — the schedule expansion resolves it to its canonical name across media
    /// sources. Both foreign keys cascade: deleting the rule or the genre removes the link.
    /// </summary>
    public class ProgramRuleGenre
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
        /// Gets or sets the genre identifier.
        /// </summary>
        [ForeignKey(nameof(Genre))]
        public long GenreId { get; set; }

        /// <summary>
        /// Gets or sets the genre.
        /// </summary>
        public Genre Genre { get; set; } = null!;
    }
}
