using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The channel-wide youth-protection rule of the program schedule ("Jugendschutz-Regelung"):
/// the window schema plus the behavior for material without a known rating. The two settings
/// form a single unit — a title's allowance at a slot start is always decided by both
/// together (<see cref="ProgramScheduleAgeWindow.IsAllowedAt"/>) and they are never used
/// apart, so they travel as one value through the materialization, selection and
/// reproducibility paths.
/// </summary>
/// <param name="Windows">The configured window schema.</param>
/// <param name="UnknownMode">The behavior for unknown ratings.</param>
public sealed record ProgramScheduleAgeWindowRule(
    ProgramScheduleAgeWindowSettings Windows,
    ProgramScheduleUnknownRatingMode UnknownMode);
