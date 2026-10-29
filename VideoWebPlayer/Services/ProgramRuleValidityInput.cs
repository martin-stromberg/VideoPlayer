using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The bundled validity input of a <see cref="ProgramRule"/> — the mode plus every window field
/// the modes use; only the mode-matching fields are validated and persisted, the rest is
/// normalized away. <see langword="null"/> on <see cref="ProgramRuleInput.Validity"/> means
/// <see cref="ProgramRuleValidityMode.Unrestricted"/>.
/// </summary>
/// <param name="Mode">The validity mode.</param>
/// <param name="From">Window start — <see cref="ProgramRuleValidityMode.OneTime"/>: real date
/// (optional); <see cref="ProgramRuleValidityMode.AnnualWindow"/>: month-day in the leap-year
/// convention 2000 (required).</param>
/// <param name="To">Inclusive window end — same convention as <paramref name="From"/>.</param>
/// <param name="FromDay">Start day-of-month (1–31) for <see cref="ProgramRuleValidityMode.MonthlyWindow"/>.</param>
/// <param name="ToDay">Inclusive end day-of-month (1–31) for
/// <see cref="ProgramRuleValidityMode.MonthlyWindow"/>; below <paramref name="FromDay"/> wraps
/// over the turn of the month.</param>
/// <param name="Weekdays">Days-of-week bitmask (bit <c>(int)DayOfWeek</c>) for
/// <see cref="ProgramRuleValidityMode.WeeklyWindow"/>.</param>
/// <param name="HolidayAnchor">Named holiday anchor for
/// <see cref="ProgramRuleValidityMode.HolidayWindow"/>.</param>
/// <param name="HolidayBeforeDays">Days before the anchor for
/// <see cref="ProgramRuleValidityMode.HolidayWindow"/> (0–<see cref="ProgramRuleValidity.MaxHolidayWindowDays"/>).</param>
/// <param name="HolidayAfterDays">Days after the anchor for
/// <see cref="ProgramRuleValidityMode.HolidayWindow"/> (0–<see cref="ProgramRuleValidity.MaxHolidayWindowDays"/>).</param>
public sealed record ProgramRuleValidityInput(
    ProgramRuleValidityMode Mode,
    DateOnly? From = null,
    DateOnly? To = null,
    int? FromDay = null,
    int? ToDay = null,
    int? Weekdays = null,
    ProgramRuleHolidayAnchor? HolidayAnchor = null,
    int? HolidayBeforeDays = null,
    int? HolidayAfterDays = null);
