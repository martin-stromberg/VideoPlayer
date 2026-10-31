using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// A named group of placeholder clips ("Platzhalter-Clip-Gruppe", e.g. mascots/interstitials)
    /// that can be assigned to <see cref="ProgramRule"/>s via <see cref="ProgramRuleClipGroup"/>.
    /// Clips from the assigned groups fill the remainder of a slot-based airing once the remaining
    /// time falls below the rule's <see cref="ProgramRule.PlaceholderGrace"/>.
    /// The reserved system group <see cref="TrailerGroupName"/> (<see cref="TrailerGroupId"/>) is
    /// seeded by migration, cannot be modified and is filled automatically with trailers of movies
    /// airing within the trailer lead window.
    /// </summary>
    public class ProgramClipGroup
    {
        /// <summary>
        /// The identifier of the reserved system group "Trailer".
        /// </summary>
        public const long TrailerGroupId = 1;

        /// <summary>
        /// The name of the reserved system group; reserved for user-created groups, too.
        /// </summary>
        public const string TrailerGroupName = "Trailer";

        /// <summary>
        /// The maximum allowed length of <see cref="Name"/>.
        /// </summary>
        public const int NameMaxLength = 255;

        /// <summary>
        /// Gets or sets the group identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the group name (unique, case-insensitive).
        /// </summary>
        [Required]
        [MaxLength(NameMaxLength)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a reserved system group that is filled
        /// automatically and cannot be renamed, deleted or manually assigned clips.
        /// </summary>
        public bool IsSystem { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the last update timestamp.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
