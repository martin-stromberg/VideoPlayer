using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The administrator-submitted attributes of a <see cref="ProgramRule"/>, shared by the create and
/// update operations of <see cref="IProgramRuleService"/>.
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
    bool IsActive);
