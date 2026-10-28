using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Evaluates the validity window of a <see cref="ProgramRule"/> for a given local day — the
/// single criterion shared by the schedule generation (<c>BuildOccurrences</c> and the filler
/// selection in <c>FillGaps</c>), the administration list grouping and the automatic purge of
/// expired one-off rules. All bounds are inclusive; all comparisons run on local days
/// (<see cref="TimeZoneInfo.Local"/>-derived).
/// </summary>
public static class ProgramRuleValidity
{
    /// <summary>
    /// The largest distance in days a <see cref="ProgramRuleValidityMode.HolidayWindow"/> may
    /// reach in either direction around its anchor.
    /// </summary>
    public const int MaxHolidayWindowDays = 62;

    /// <summary>
    /// Whether the rule is valid on the given local day, evaluated per
    /// <see cref="ProgramRule.ValidityMode"/>: <c>Unrestricted</c> always; <c>OneTime</c> inside
    /// the inclusive date range with either bound open (a rule without any bound is invalid —
    /// protects against corrupt data); <c>AnnualWindow</c> compares month-day ordinals and wraps
    /// over the turn of the year when Von &gt; Bis; <c>MonthlyWindow</c> compares day-of-month
    /// ordinals and wraps over the turn of the month when Von &gt; Bis (a missing day like the
    /// 31st simply never matches); <c>WeeklyWindow</c> bit-tests the day's
    /// <see cref="DateOnly.DayOfWeek"/>; <c>HolidayWindow</c> checks the anchor window
    /// <c>anchor − before ≤ day ≤ anchor + after</c> for the years <c>day.Year − 1 … + 1</c>
    /// (covers windows crossing the turn of the year).
    /// </summary>
    /// <param name="rule">The rule whose validity window is evaluated.</param>
    /// <param name="localDay">The local day to test.</param>
    /// <returns><see langword="true"/> when the day lies inside the rule's validity window.</returns>
    public static bool IsValidOn(ProgramRule rule, DateOnly localDay)
    {
        switch (rule.ValidityMode)
        {
            case ProgramRuleValidityMode.Unrestricted:
                return true;

            case ProgramRuleValidityMode.OneTime:
                if (rule.ValidFrom is null && rule.ValidTo is null)
                    return false;
                return (rule.ValidFrom is null || rule.ValidFrom.Value <= localDay)
                    && (rule.ValidTo is null || localDay <= rule.ValidTo.Value);

            case ProgramRuleValidityMode.AnnualWindow:
            {
                if (rule.ValidFrom is null || rule.ValidTo is null)
                    return false;
                var from = rule.ValidFrom.Value.Month * 100 + rule.ValidFrom.Value.Day;
                var to = rule.ValidTo.Value.Month * 100 + rule.ValidTo.Value.Day;
                var day = localDay.Month * 100 + localDay.Day;
                return from <= to ? day >= from && day <= to : day >= from || day <= to;
            }

            case ProgramRuleValidityMode.MonthlyWindow:
            {
                if (rule.ValidFromDay is null || rule.ValidToDay is null)
                    return false;
                var from = rule.ValidFromDay.Value;
                var to = rule.ValidToDay.Value;
                return from <= to ? localDay.Day >= from && localDay.Day <= to
                    : localDay.Day >= from || localDay.Day <= to;
            }

            case ProgramRuleValidityMode.WeeklyWindow:
                return rule.ValidWeekdays is { } mask
                    && mask is >= 1 and <= 127
                    && (mask & (1 << (int)localDay.DayOfWeek)) != 0;

            case ProgramRuleValidityMode.HolidayWindow:
            {
                if (rule.ValidHolidayAnchor is not { } anchor || !Enum.IsDefined(anchor)
                    || rule.ValidHolidayBeforeDays is not { } before
                    || rule.ValidHolidayAfterDays is not { } after)
                    return false;
                for (var year = localDay.Year - 1; year <= localDay.Year + 1; year++)
                {
                    var anchorDate = GermanHolidayCalendar.GetAnchorDate(anchor, year);
                    if (anchorDate.AddDays(-before) <= localDay && localDay <= anchorDate.AddDays(after))
                        return true;
                }
                return false;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the rule's one-off validity window has fully passed — a
    /// <see cref="ProgramRuleValidityMode.OneTime"/> rule whose <see cref="ProgramRule.ValidTo"/>
    /// lies before the given local day. One-off rules without a Bis date (single-sided open) and
    /// every other mode never expire.
    /// </summary>
    /// <param name="rule">The rule to test.</param>
    /// <param name="localDay">The local day to compare against.</param>
    /// <returns><see langword="true"/> when the rule's validity has run out for good.</returns>
    public static bool IsExpired(ProgramRule rule, DateOnly localDay)
        => rule.ValidityMode == ProgramRuleValidityMode.OneTime
            && rule.ValidTo is { } to
            && to < localDay;
}
