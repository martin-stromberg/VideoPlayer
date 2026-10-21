using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a named automated TV channel ("Sender"). The model is channel-capable, but exactly
    /// one channel is shipped and manageable for now (<see cref="ProgramRule"/> rows belong to a
    /// channel instead of being global, so more channels can be added later without schema changes).
    /// </summary>
    public class TVChannel
    {
        /// <summary>
        /// The maximum allowed length of <see cref="Name"/>.
        /// </summary>
        public const int NameMaxLength = 255;

        /// <summary>
        /// Gets or sets the channel identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the channel name.
        /// </summary>
        [Required]
        [MaxLength(NameMaxLength)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the program rules belonging to this channel.
        /// </summary>
        public ICollection<ProgramRule> ProgramRules { get; set; } = new List<ProgramRule>();
    }
}
