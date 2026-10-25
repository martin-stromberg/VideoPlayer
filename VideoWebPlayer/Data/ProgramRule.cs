using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents a program rule ("Programmregel") of a <see cref="TVChannel"/>: a recurring broadcast
    /// described by its rule type, recurrence pattern, air time and desired length or episode count,
    /// the assigned program material and a priority used to resolve conflicts between rules.
    /// </summary>
    public class ProgramRule
    {
        /// <summary>
        /// The maximum allowed length of <see cref="Name"/>.
        /// </summary>
        public const int NameMaxLength = 255;

        /// <summary>
        /// The default placeholder grace period ("Karenzzeit") applied to rules with assigned
        /// clip groups when no explicit value is submitted.
        /// </summary>
        public static readonly TimeSpan DefaultPlaceholderGrace = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Gets or sets the rule identifier.
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
        /// Gets or sets the rule name.
        /// </summary>
        [Required]
        [MaxLength(NameMaxLength)]
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the rule type (series slot, prime-time movie, genre rotation, repeat,
        /// holiday special or filler).
        /// </summary>
        public ProgramRuleType RuleType { get; set; }

        /// <summary>
        /// Gets or sets the recurrence pattern (daily, weekly, once, on holidays, on weekdays
        /// or on weekends).
        /// </summary>
        public ProgramRuleRecurrence Recurrence { get; set; }

        /// <summary>
        /// Gets or sets the day of week the rule airs on; required iff
        /// <see cref="Recurrence"/> is <see cref="ProgramRuleRecurrence.Weekly"/>.
        /// </summary>
        public DayOfWeek? DayOfWeek { get; set; }

        /// <summary>
        /// Gets or sets the single air date; required iff <see cref="Recurrence"/> is
        /// <see cref="ProgramRuleRecurrence.Once"/>.
        /// </summary>
        public DateOnly? SingleDate { get; set; }

        /// <summary>
        /// Gets or sets the desired air time of day.
        /// </summary>
        public TimeOnly TimeOfDay { get; set; }

        /// <summary>
        /// Gets or sets the number of episodes per airing for series-based rules, or
        /// <see langword="null"/> when <see cref="SlotDuration"/> is used instead. At least one of
        /// the two fields must be set.
        /// </summary>
        public int? EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets the desired length of the broadcast slot, or <see langword="null"/> when
        /// <see cref="EpisodeCount"/> is used instead. At least one of the two fields must be set.
        /// </summary>
        public TimeSpan? SlotDuration { get; set; }

        /// <summary>
        /// Gets or sets the kind of the assigned program material (series, movie, movie collection,
        /// genre or user playlist); exactly one material reference per rule, polymorphic like
        /// <see cref="PlaylistEntry.MediaType"/>/<see cref="PlaylistEntry.MediaId"/> — deliberately no
        /// hard foreign key into the media tables.
        /// </summary>
        public ProgramRuleMaterialType MaterialType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the assigned program material within the entity set
        /// selected by <see cref="MaterialType"/>. For <see cref="ProgramRuleMaterialType.Genre"/>
        /// rules this holds the first selected genre; the full genre selection lives in
        /// <see cref="Genres"/>.
        /// </summary>
        public long MaterialId { get; set; }

        /// <summary>
        /// Gets or sets the genres selected for <see cref="ProgramRuleMaterialType.Genre"/>
        /// material (empty for every other material type).
        /// </summary>
        public ICollection<ProgramRuleGenre> Genres { get; set; } = new List<ProgramRuleGenre>();

        /// <summary>
        /// Gets or sets the media-source filter used by
        /// <see cref="ProgramRuleMaterialType.Genre"/> material; empty means all media sources.
        /// </summary>
        public ICollection<ProgramRuleMediaSource> MediaSourceFilters { get; set; } = new List<ProgramRuleMediaSource>();

        /// <summary>
        /// Gets or sets the clip groups assigned to this rule (independent of the material type;
        /// only meaningful for non-filler rules with a <see cref="SlotDuration"/>).
        /// </summary>
        public ICollection<ProgramRuleClipGroup> ClipGroups { get; set; } = new List<ProgramRuleClipGroup>();

        /// <summary>
        /// Gets or sets the placeholder grace period ("Karenzzeit"): once the remaining time of a
        /// <see cref="SlotDuration"/> period falls below this value, no further title is started
        /// and the remainder is filled with clips from <see cref="ClipGroups"/> instead.
        /// </summary>
        public TimeSpan PlaceholderGrace { get; set; } = DefaultPlaceholderGrace;

        /// <summary>
        /// Gets or sets the priority used to resolve conflicts between competing rules
        /// (higher value wins; evaluated when the schedule is generated).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the rule is active and takes part in scheduling.
        /// </summary>
        public bool IsActive { get; set; } = true;

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
