using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The administrator-submitted attributes of a <see cref="ProgramRule"/>, shared by the create and
/// update operations of <see cref="IProgramRuleService"/>. For
/// <see cref="ProgramRuleMaterialType.Genre"/> material, <c>GenreIds</c> carries the selected
/// genres (at least one required) and <c>MediaSourceIds</c> the optional source filter (empty
/// means all sources); both lists are rejected for every other material type. <c>MaterialId</c>
/// remains the single material reference for non-genre materials.
/// </summary>
public sealed record ProgramRuleInput(
    string Name,
    ProgramRuleType RuleType,
    ProgramRuleRecurrence Recurrence,
    DayOfWeek? DayOfWeek,
    DateOnly? SingleDate,
    TimeOnly TimeOfDay,
    int? EpisodeCount,
    TimeSpan? SlotDuration,
    ProgramRuleMaterialType MaterialType,
    long MaterialId,
    int Priority,
    bool IsActive,
    IReadOnlyList<long>? GenreIds = null,
    IReadOnlyList<long>? MediaSourceIds = null,
    IReadOnlyList<long>? ClipGroupIds = null,
    TimeSpan? PlaceholderGrace = null,
    ProgramRuleValidityInput? Validity = null);
